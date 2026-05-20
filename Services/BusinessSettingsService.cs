using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using CamperoDesktop.Models;

namespace CamperoDesktop.Services;

public class BusinessSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public BusinessSettingsModel Load()
    {
        string path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return BuildDefaultModel();
        }

        JsonNode root = JsonNode.Parse(File.ReadAllText(path)) ?? new JsonObject();
        JsonNode? business = root["Business"];

        return new BusinessSettingsModel
        {
            Name = business?["Name"]?.GetValue<string>() ?? BusinessBranding.BusinessName,
            Address = business?["Address"]?.GetValue<string>() ?? BusinessBranding.Address,
            Phone = business?["Phone"]?.GetValue<string>() ?? BusinessBranding.Phone,
            Nit = business?["Nit"]?.GetValue<string>() ?? BusinessBranding.Nit,
            Email = business?["Email"]?.GetValue<string>() ?? BusinessBranding.Email,
            Branch = business?["Branch"]?.GetValue<string>() ?? BusinessBranding.Branch,
            FooterMessage = business?["FooterMessage"]?.GetValue<string>() ?? BusinessBranding.FooterMessage,
            LogoPath = business?["LogoPath"]?.GetValue<string>() ?? "assets/logo.png"
        };
    }

    public void Save(BusinessSettingsModel model)
    {
        string path = GetSettingsPath();
        JsonObject root;

        if (File.Exists(path))
        {
            root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        root["Business"] = new JsonObject
        {
            ["Name"] = model.Name.Trim(),
            ["Address"] = model.Address.Trim(),
            ["Phone"] = model.Phone.Trim(),
            ["Nit"] = model.Nit.Trim(),
            ["Email"] = model.Email.Trim(),
            ["Branch"] = model.Branch.Trim(),
            ["FooterMessage"] = model.FooterMessage.Trim(),
            ["LogoPath"] = model.LogoPath.Trim()
        };

        File.WriteAllText(path, root.ToJsonString(SerializerOptions));
        BusinessBranding.Reload();
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    private static BusinessSettingsModel BuildDefaultModel()
    {
        return new BusinessSettingsModel
        {
            Name = BusinessBranding.BusinessName,
            Address = BusinessBranding.Address,
            Phone = BusinessBranding.Phone,
            Nit = BusinessBranding.Nit,
            Email = BusinessBranding.Email,
            Branch = BusinessBranding.Branch,
            FooterMessage = BusinessBranding.FooterMessage,
            LogoPath = "assets/logo.png"
        };
    }
}
