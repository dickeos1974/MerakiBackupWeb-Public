using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MerakiBackupWeb.Services;

public class MerakiApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public MerakiApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;

        var apiKey = _configuration["Meraki:ApiKey"];
        var baseUrl = _configuration["Meraki:BaseUrl"];

        _httpClient.BaseAddress = new Uri(baseUrl!);
        _httpClient.DefaultRequestHeaders.Add("X-Cisco-Meraki-API-Key", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<string> GetOrganizationsRawAsync()
    {
        var response = await _httpClient.GetAsync("organizations");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
    public async Task<string> GetNetworksRawAsync(string organizationId)
    {
        var response = await _httpClient.GetAsync($"organizations/{organizationId}/networks");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
    public async Task<string> GetSsidsRawAsync(string networkId)
    {
        var response = await _httpClient.GetAsync($"networks/{networkId}/wireless/ssids");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
    public async Task<string> GetVlansRawAsync(string networkId)
    {
        var response = await _httpClient.GetAsync($"networks/{networkId}/appliance/vlans");

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
    public async Task<string> GetL3FirewallRulesRawAsync(string networkId)
    {
        var response = await _httpClient.GetAsync($"networks/{networkId}/appliance/firewall/l3FirewallRules");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
    public async Task<string> GetSwitchPortsRawAsync(string serial)
    {
        var response = await _httpClient.GetAsync($"devices/{serial}/switch/ports");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
    public async Task<string> GetDevicesRawAsync(string networkId)
    {
        var response = await _httpClient.GetAsync($"networks/{networkId}/devices");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
    //UPDATE Stuff
    // SSID's
    public async Task<string> UpdateSsidRawAsync(string networkId, string ssidNumber, object payload)
    {
        var json = JsonSerializer.Serialize(payload);

        var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PutAsync(
            $"networks/{networkId}/wireless/ssids/{ssidNumber}",
            content
        );

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"Meraki restore failed: {(int)response.StatusCode} {response.ReasonPhrase}. Response: {responseBody}. Payload: {json}"
            );
        }

        return responseBody;
    }
    public async Task<bool> NetworkHasTagAsync(string organizationId, string networkId, string requiredTag)
    {
        var networksJson = await GetNetworksRawAsync(organizationId);

        var networks = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(networksJson);

        if (networks == null)
            return false;

        var network = networks.FirstOrDefault(n =>
            n.ContainsKey("id") &&
            n["id"]?.ToString() == networkId
        );

        if (network == null || !network.ContainsKey("tags"))
            return false;

        var tagsText = network["tags"]?.ToString() ?? "";

        return tagsText.Contains(requiredTag, StringComparison.OrdinalIgnoreCase);
    }
    public async Task<string> UpdateVlanAsync(string networkId, string vlanId, object payload)
    {
        var json = JsonSerializer.Serialize(payload);

        var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PutAsync(
            $"networks/{networkId}/appliance/vlans/{vlanId}",
            content
        );

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"VLAN restore failed: {(int)response.StatusCode} {responseBody}"
            );
        }

        return responseBody;
    }
    public async Task<string> CreateVlanAsync(string networkId, object payload)
    {
        var json = JsonSerializer.Serialize(payload);

        var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(
            $"networks/{networkId}/appliance/vlans",
            content
        );

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"VLAN create failed: {(int)response.StatusCode} {responseBody}"
            );
        }

        return responseBody;
    }
    public async Task<string> UpdateFirewallRulesAsync(string networkId, object payload)
    {
        var json = JsonSerializer.Serialize(payload);

        var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PutAsync(
            $"networks/{networkId}/appliance/firewall/l3FirewallRules",
            content
        );

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"Firewall restore failed: {(int)response.StatusCode} {responseBody}"
            );
        }

        return responseBody;
    }
    public async Task<string> UpdateSwitchPortAsync(string serial, string portId, object payload)
    {
        var json = JsonSerializer.Serialize(payload);

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync(
            $"devices/{serial}/switch/ports/{portId}",
            content
        );

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Switch port restore failed: {(int)response.StatusCode} {body}");

        return body;
    }
}