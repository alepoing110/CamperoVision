using MySqlConnector;

namespace CamperoDesktop.Helpers;

public static class DataReaderExtensions
{
    public static string GetStringSafe(this MySqlDataReader reader, string columnName, string defaultValue = "")
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? defaultValue : reader.GetString(ordinal);
    }

    public static string GetStringSafe(this MySqlDataReader reader, int ordinal, string defaultValue = "")
    {
        return reader.IsDBNull(ordinal) ? defaultValue : reader.GetString(ordinal);
    }

    public static bool IsColumnNull(this MySqlDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal);
    }

    public static bool IsColumnNull(this MySqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal);
    }

    public static int? GetNullableInt32(this MySqlDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    public static int? GetNullableInt32(this MySqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    public static decimal? GetNullableDecimal(this MySqlDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    public static decimal? GetNullableDecimal(this MySqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    public static DateTime? GetNullableDateTime(this MySqlDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    public static DateTime? GetNullableDateTime(this MySqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }
}
