namespace Fx { public class C {
#if NET472
  public static string S() => typeof (System.Management.ManagementObject).Name;
#endif
} }
