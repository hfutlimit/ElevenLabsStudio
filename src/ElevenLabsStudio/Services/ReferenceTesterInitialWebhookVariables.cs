namespace ElevenLabsStudio.Services;

public sealed record InitialWebhookVariableScenario(
	string Key,
	string DisplayName,
	IReadOnlyDictionary<string, object?> Variables);

public static class ReferenceTesterInitialWebhookVariables
{
	public static IReadOnlyList<InitialWebhookVariableScenario> Scenarios { get; } =
	[
		new(
			"found",
			"Account found — pass all variables",
			new Dictionary<string, object?>
			{
				["lookup_status"] = "found",
				["contact_name"] = "Clinton Smith5",
				["organization_by_phone"] = "ZYX Sample Client - tuplus01qa",
				["client_id_by_phone"] = "tuplus01qa",
				["fallback_client_id"] = "supportteam",
				["email_by_phone"] = "csmith+5@transfinder.com",
				["account_id_by_phone"] = "56f9282d-a970-f111-842b-0e9e16a6d2ed",
				["contact_id_by_phone"] = "be1f0915-a17b-f111-842b-0e9e16a6d2ed",
				["caller_id_norm"] = "2601234567",
			}),
		new(
			"not_found",
			"Account not found — fallback values",
			new Dictionary<string, object?>
			{
				["lookup_status"] = "not_found",
				["contact_name"] = string.Empty,
				["organization_by_phone"] = string.Empty,
				["client_id_by_phone"] = "tuplus01qa",
				["fallback_client_id"] = "tuplus01qa",
				["email_by_phone"] = string.Empty,
				["account_id_by_phone"] = string.Empty,
				["contact_id_by_phone"] = string.Empty,
				["caller_id_norm"] = "2601234567",
			}),
	];
}
