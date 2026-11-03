#!/usr/bin/env python3
"""Cut the Xake.Dotnet part of the Xake history, with history kept.

Usage:
    python3 tools/import-from-xake.py <xake-clone> <commit> [<ref>]

<xake-clone> is a scratch clone of OlegZee/Xake (it receives the new ref; never run this on a
working checkout), <commit> the Xake commit to cut at, <ref> the ref to write (default
refs/heads/cut). The script

1. runs `git fast-export --no-data` of <commit> in <xake-clone>,
2. keeps only the paths below (renaming the src/tests ones to src/dotnet.tests/), drops commits
   left without changes, collapses the parents of dropped commits, rewrites the pull request
   references in commit subjects (a trailing "(#N)" to "(OlegZee/Xake#N)", decision S-D6, and
   "Merge pull request #N from ..." to "Merge pull request OlegZee/Xake#N from ...", S-D13), and
   maps the owner's work address to NOREPLY (S-D12, below), and replaces former product and
   company names in old file versions by neutral ones (S-D16, below),
3. feeds the result to `git fast-import` onto <ref> in the same clone (the blobs are already there),
4. checks that the tree of <ref> equals the tree of <commit> mapped through the same path rules
   (paths and blob ids) and prints the numbers.

Then, in Xake.Dotnet: `git fetch <xake-clone> <ref>` and merge FETCH_HEAD
(--allow-unrelated-histories the first time); no local branch is kept for the cut (S-D15).

The work address (S-D12). An address whose domain, lowercased, has the SHA-256 in WORK_DOMAINS
is replaced by NOREPLY in the author and committer lines and anywhere in the message (the
Co-authored-by trailers); the name is kept. The domain is matched by its hash so that this file
does not carry it; nothing else is needed to rerun the script, no environment, no mailmap.

The names (S-D16). Old versions of the hermetic docs and tests (before Xake #60) name a former
employer's packages, products and a repository. Every word of a blob (letters and digits, joined
by hyphens) whose lowercased SHA-256 is a key of NAMES is replaced by its value (lowercase when
the word is lowercase); the blob is then written inline. Only file contents change; the cut tip
must need no replacement (the tree check of step 4 fails otherwise), so the current tree is the
one Xake has. Matching by hash keeps the names out of this file.

Rerun in S6: a fresh `git clone --no-local` of OlegZee/Xake into a scratch folder, then
`python3 tools/import-from-xake.py <clone> <new commit>`; the commits up to the S4 cut come out
with the same SHAs, so merging the new ref brings in only the later Xake commits.

The output depends only on <commit> and on this file: the same input gives the same SHAs, which
is what lets a later cut (S6) merge on top of an earlier one. Do not change PREFIXES, EXACT,
TESTS, the subject rewrite or the address mapping between imports; add a new path as a plain copy instead.
"""
import hashlib
import re
import subprocess
import sys

PREFIXES = [
    "src/dotnet/", "src/hermetic/", "src/hermetic.tests/", "docs/hermetic/",
    "locks/Xake.Dotnet/", "locks/Xake.Hermetic.Dotnet/", "samples/hermetic/", "samples/netcore-compile/",
]
EXACT = set("""
docs/schemas/xake.project.schema.json docs/dotnet-build.md docs/csc-server.md docs/csc-syntax.md
docs/description.md docs/hermetic-guide.md docs/hermetic-architecture.md docs/fingerprints.md
build.fsc.fsx testjobs.fsx xake.fingerprints
samples/fullframework.fsx samples/toolset.fsx samples/dotnet-fake.csproj samples/helloworld.cs
""".split())
TESTS = set("src/tests/%s.fs" % t for t in """
DotnetTasksTests ToolsetTests PackageRootTests SdkProbeTests CscServerTests ResxTests
CompilerCrashTests CompilerJobsTests FscTests PathSpacesTests HermeticModeTests DescriptionTests
ProjectPlanTests LibraryVarsTests RecordShapeTests Common XakeTestBase
""".split())
TESTS.add("src/tests/tests.fsproj")
TEST_DIRS = ["src/tests/fixtures/description/"]

SUBJECT_PR = re.compile(rb"\(#(\d+)\)(\s*)$")
SUBJECT_MERGE = re.compile(rb"^Merge pull request #(\d+) from ")

NOREPLY = b"513301+OlegZee@users.noreply.github.com"
WORK_DOMAINS = {"dd8c83a88725b42d221726aeda59b2167fe3e8a3d47d21b354ec60c52fd7e95d"}
NAMES = {
    "47f54bcf8f7a9bbe857c6e451f359f830c4a810c85edd0b7941f63a95e9da9ea": b"Acme",
    "e035b0cf8591b7800a348365e8f1ed8b2249879f11b27e81bb9629a233a1abe4": b"Initech",
    "87dc71bf793febc25154db8872bf4650dfa95414a96b7436c2b985c973c37724": b"Globex",
    "7eebf045425206f7b1676ed26af253b31fe9dc01eefc81bf5ec809cb58a431bd": b"AcmeReports",
    "049dd8c77e9b1b2975324bc9cb381411aa0af2e4ebb71e173f408116d230dc62": b"acme-data-engine",
}
WORD = re.compile(rb"[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*")
ADDRESS = re.compile(rb"<([^<>@\s]+)@([^<>@\s]+)>")


def work_address(m):
    return hashlib.sha256(m.group(2).lower()).hexdigest() in WORK_DOMAINS


def map_addresses(text):
    """Replace every <user@work-domain> in text by <NOREPLY>."""
    return ADDRESS.sub(lambda m: b"<" + NOREPLY + b">" if work_address(m) else m.group(0), text)


def mapped(p):
    """The new path of Xake path p, or None when p is not imported."""
    if p in TESTS or any(p.startswith(d) for d in TEST_DIRS):
        return "src/dotnet.tests/" + p[len("src/tests/"):]
    if p in EXACT or any(p.startswith(x) for x in PREFIXES):
        return p
    return None


def neutral(m):
    w = m.group(0)
    r = NAMES.get(hashlib.sha256(w.lower()).hexdigest())
    if r is None:
        return w
    return r.lower() if w.islower() else r


class Blobs:
    """Blob contents by id (git cat-file --batch), the S-D16 replacement cached per blob."""

    def __init__(self, repo):
        self.cat = subprocess.Popen(["git", "-C", repo, "cat-file", "--batch"],
                                    stdin=subprocess.PIPE, stdout=subprocess.PIPE)
        self.done = {}

    def rewritten(self, sha):
        """The new content of blob sha, or None when it needs no replacement."""
        if sha not in self.done:
            self.cat.stdin.write(sha.encode() + b"\n")
            self.cat.stdin.flush()
            size = int(self.cat.stdout.readline().split()[2])
            data = self.cat.stdout.read(size)
            self.cat.stdout.read(1)
            new = None if b"\0" in data else WORD.sub(neutral, data)
            self.done[sha] = new if new is not None and new != data else None
        return self.done[sha]

    def close(self):
        self.cat.stdin.close()
        self.cat.wait()


def unquote(raw):
    if not raw.startswith('"'):
        return raw
    return raw[1:-1].encode("latin1").decode("unicode_escape").encode("latin1").decode("utf-8")


def quote(p):
    if any(c in p for c in '"\\\n') or p.startswith(" "):
        return '"' + p.replace("\\", "\\\\").replace('"', '\\"') + '"'
    return p


def rewrite_message(msg):
    """Rewrite the PR references of the subject line, map work addresses in the whole message."""
    first, nl, rest = msg.partition(b"\n")
    first = SUBJECT_PR.sub(rb"(OlegZee/Xake#\1)\2", first, count=1)
    first = SUBJECT_MERGE.sub(rb"Merge pull request OlegZee/Xake#\1 from ", first, count=1)
    return map_addresses(first + nl + rest)


def filter_stream(inp, out, ref, blobs):
    alias = {}  # dropped commit mark -> the mark that replaces it (None: no parent)
    stats = {"in": 0, "kept": 0, "rewritten": 0, "mapped": 0}
    neutralized = set()

    def resolve(m):
        while m in alias:
            m = alias[m]
        return m

    line = inp.readline()
    while line:
        if line.startswith(b"commit "):
            stats["in"] += 1
            hdr, mark, parents, changes, deleteall = [], None, [], [], False
            line = inp.readline()
            while line:
                s = line.decode("utf-8", "surrogateescape")
                if s.startswith("mark "):
                    mark = s.split()[1]
                elif s.startswith(("author ", "committer ")):
                    new = map_addresses(line)
                    if new != line:
                        stats["mapped"] += 1
                    hdr.append(new)
                elif s.startswith(("original-oid ", "encoding ")):
                    hdr.append(line)
                elif s.startswith("data "):
                    msg = inp.read(int(s.split()[1]))
                    new = rewrite_message(msg)
                    if new != msg:
                        stats["rewritten"] += 1
                    if map_addresses(msg) != msg:
                        stats["mapped"] += 1
                    hdr.append(b"data %d\n" % len(new) + new)
                    line = inp.readline()
                    if line == b"\n":
                        line = inp.readline()
                    continue
                elif s.startswith(("from ", "merge ")):
                    parents.append(s.split()[1])
                elif s.startswith("M "):
                    mode, sha, raw = s.rstrip("\n").split(" ", 3)[1:]
                    p = mapped(unquote(raw))
                    if p:
                        new = blobs.rewritten(sha)
                        if new is None:
                            changes.append(("M %s %s %s\n" % (mode, sha, quote(p))).encode("utf-8", "surrogateescape"))
                        else:
                            neutralized.add(sha)
                            changes.append(("M %s inline %s\n" % (mode, quote(p))).encode("utf-8", "surrogateescape")
                                           + b"data %d\n" % len(new) + new + b"\n")
                elif s.startswith("D "):
                    p = mapped(unquote(s.rstrip("\n")[2:]))
                    if p:
                        changes.append(("D %s\n" % quote(p)).encode("utf-8", "surrogateescape"))
                elif s.startswith("deleteall"):
                    deleteall = True
                else:
                    break
                line = inp.readline()
            ps = []
            for p in parents:
                r = resolve(p)
                if r is not None and r not in ps:
                    ps.append(r)
            if not changes and not deleteall and len(ps) <= 1:
                alias[mark] = ps[0] if ps else None
                continue
            stats["kept"] += 1
            if not ps:
                out.write(b"reset %s\n" % ref)
            out.write(b"commit %s\nmark %s\n" % (ref, mark.encode()))
            for h in hdr:
                out.write(h)
            if not hdr[-1].endswith(b"\n"):
                out.write(b"\n")
            if ps:
                out.write(b"from %s\n" % ps[0].encode())
                for p in ps[1:]:
                    out.write(b"merge %s\n" % p.encode())
            if deleteall:
                out.write(b"deleteall\n")
            for c in changes:
                out.write(c)
            out.write(b"\n")
            continue
        if line.startswith(b"tag "):  # not expected for a commit argument; skip with its message
            line = inp.readline()
            while line and not line.startswith(b"data "):
                line = inp.readline()
            inp.read(int(line.split()[1]))
        line = inp.readline()
    stats["neutral"] = len(neutralized)
    return stats


def git(repo, *args, **kw):
    return subprocess.run(["git", "-C", repo] + list(args), check=True, **kw)


def tree(repo, rev):
    out = git(repo, "ls-tree", "-r", "-z", rev, stdout=subprocess.PIPE).stdout.decode("utf-8")
    d = {}
    for entry in filter(None, out.split("\0")):
        meta, p = entry.split("\t", 1)
        mode, _, sha = meta.split()
        d[p] = (mode, sha)
    return d


def main(argv):
    if len(argv) not in (3, 4):
        sys.stderr.write(__doc__)
        return 2
    repo, commit = argv[1], argv[2]
    ref = (argv[3] if len(argv) == 4 else "refs/heads/cut").encode()
    commit = git(repo, "rev-parse", "--verify", commit + "^{commit}", stdout=subprocess.PIPE).stdout.decode().strip()

    exp = subprocess.Popen(["git", "-C", repo, "fast-export", "--no-data", "--signed-tags=strip",
                            "--tag-of-filtered-object=drop", "--reencode=no", commit], stdout=subprocess.PIPE)
    imp = subprocess.Popen(["git", "-C", repo, "fast-import", "--quiet", "--force"], stdin=subprocess.PIPE)
    blobs = Blobs(repo)
    stats = filter_stream(exp.stdout, imp.stdin, ref, blobs)
    blobs.close()
    imp.stdin.close()
    if exp.wait() != 0 or imp.wait() != 0:
        sys.stderr.write("fast-export/fast-import failed\n")
        return 1

    expected = {}
    for p, v in tree(repo, commit).items():
        q = mapped(p)
        if q:
            expected[q] = v
    got = tree(repo, ref.decode())
    missing = sorted(set(expected) - set(got))
    extra = sorted(set(got) - set(expected))
    differ = sorted(p for p in expected if p in got and got[p] != expected[p])
    tip = git(repo, "rev-parse", ref.decode(), stdout=subprocess.PIPE).stdout.decode().strip()
    merges = git(repo, "rev-list", "--count", "--merges", ref.decode(), stdout=subprocess.PIPE).stdout.decode().strip()
    print("cut at      %s" % commit)
    print("commits     %d in, %d kept (%s merges), %d messages rewritten" % (stats["in"], stats["kept"], merges, stats["rewritten"]))
    print("addresses   %d author/committer lines and messages mapped to %s" % (stats["mapped"], NOREPLY.decode()))
    print("names       %d blobs rewritten with neutral names" % stats["neutral"])
    print("files       %d expected, %d in %s" % (len(expected), len(got), ref.decode()))
    print("differences %d missing, %d extra, %d differing" % (len(missing), len(extra), len(differ)))
    for p in missing[:10] + extra[:10] + differ[:10]:
        print("  " + p)
    print("tip         %s" % tip)
    return 0 if not (missing or extra or differ) else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
