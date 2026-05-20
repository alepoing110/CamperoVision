namespace CamperoDesktop.Models;

public static class UserRoles
{
    public const string Admin = "admin";
    public const string Almacenero = "almacenero";
    public const string Vendedor = "vendedor";

    public static readonly string[] All = { Admin, Almacenero, Vendedor };
    public static readonly string[] BuilderOptions = { Admin, Vendedor, Almacenero };

    public static bool IsAdmin(string role) => role.Trim().Equals(Admin, System.StringComparison.OrdinalIgnoreCase);
    public static bool IsAlmacenero(string role) => role.Trim().Equals(Almacenero, System.StringComparison.OrdinalIgnoreCase);
    public static bool IsVendedor(string role) => role.Trim().Equals(Vendedor, System.StringComparison.OrdinalIgnoreCase);
}
