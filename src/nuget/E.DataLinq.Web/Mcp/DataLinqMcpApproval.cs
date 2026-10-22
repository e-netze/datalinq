using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Mcp;

// Server-side user approval via MCP elicitation. The agent cannot answer the dialog itself.
// Clients without elicitation support are refused.
public class DataLinqMcpApproval
{
	private const string RememberField = "remember";
	private const string InputKey = "datalinq_approval";
	private const int MaxDetailsLength = 4000;
	static private readonly TimeSpan RememberLifetime = TimeSpan.FromHours(8);

	private readonly ConcurrentDictionary<string, DateTime> _remembered = new();
	private readonly byte[] _stateKey = RandomNumberGenerator.GetBytes(32);

	// rememberKey: lets the user allow the same action on the same target for the rest of the session.
	async public Task EnsureApprovedAsync(RequestContext<CallToolRequestParams> context,
										  string action,
										  string target,
										  string details = null,
										  string rememberKey = null,
										  CancellationToken cancellationToken = default)
	{
		var server = context?.Server ?? throw new McpException(Refused(action, "there is no MCP session"));
		var hasSession = !String.IsNullOrEmpty(server.SessionId);

		var sessionKey = rememberKey != null && hasSession ? $"{server.SessionId}|{rememberKey}" : null;
		if (sessionKey != null &&
			_remembered.TryGetValue(sessionKey, out var expires) &&
			expires > DateTime.UtcNow)
		{
			return;
		}

		if (!server.IsMrtrSupported && server.ClientCapabilities?.Elicitation is null)
		{
			throw new McpException(Refused(action, "the MCP client does not support approval dialogs (elicitation)"));
		}

		var request = BuildRequest(action, target, details, offerRemember: sessionKey != null);

		ElicitResult result;
		if (!hasSession)
		{
			// Multi round-trip: the first call returns the dialog, the client retries the tool call with the answer.
			// The request state binds the answer to exactly this action, target and details.
			var state = StateFor(action, target, details);
			var p = context.Params;
			if (p?.RequestState != state ||
				p.InputResponses is null ||
				!p.InputResponses.TryGetValue(InputKey, out var response))
			{
				throw new InputRequiredException(
					new Dictionary<string, InputRequest>() { [InputKey] = InputRequest.ForElicitation(request) },
					state);
			}
			result = response.Deserialize(InputResponse.ElicitResultJsonTypeInfo);
		}
		else
		{
			try
			{
				result = await server.ElicitAsync(request, cancellationToken);
			}
			catch (Exception ex) when (ex is not OperationCanceledException and not McpException)
			{
				throw new McpException(Refused(action, $"the approval request failed ({ex.Message})"));
			}
		}

		if (result is null || !result.IsAccepted)
		{
			throw new McpException(
				$"The user did NOT approve: {action} ({target}). Nothing was executed or changed. " +
				"Do not retry automatically; ask the user what to do instead.");
		}

		if (sessionKey != null &&
			result.Content is not null &&
			result.Content.TryGetValue(RememberField, out var remember) &&
			remember.ValueKind == JsonValueKind.True)
		{
			Cleanup();
			_remembered[sessionKey] = DateTime.UtcNow.Add(RememberLifetime);
		}
	}

	static private ElicitRequestParams BuildRequest(string action, string target, string details, bool offerRemember)
	{
		// Accepting the dialog is the approval; no required fields.
		var properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>();
		if (offerRemember)
		{
			properties[RememberField] = new ElicitRequestParams.BooleanSchema()
			{
				Title = "Don't ask again for this target in this session",
				Description = $"Also allow '{action}' for {target} until this session ends (max {RememberLifetime.TotalHours:0} h).",
				Default = false
			};
		}

		return new ElicitRequestParams()
		{
			Message = BuildMessage(action, target, details),
			RequestedSchema = new ElicitRequestParams.RequestSchema() { Properties = properties }
		};
	}

	static private string BuildMessage(string action, string target, string details)
	{
		var sb = new StringBuilder();
		sb.Append("DataLinq agent wants to ").Append(action).Append(": ").Append(target);
		sb.Append("\n\nAccept/Allow to approve, Decline to reject.");

		if (!String.IsNullOrWhiteSpace(details))
		{
			var text = details.Length > MaxDetailsLength
				? details.Substring(0, MaxDetailsLength) + $"\n... ({details.Length - MaxDetailsLength} more characters)"
				: details;
			sb.Append("\n\n").Append(text);
		}

		return sb.ToString();
	}

	private string StateFor(string action, string target, string details)
	{
		using var hmac = new HMACSHA256(_stateKey);
		return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{action}\n{target}\n{details}")));
	}

	private void Cleanup()
	{
		var now = DateTime.UtcNow;
		foreach (var key in _remembered.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToArray())
		{
			_remembered.TryRemove(key, out _);
		}
	}

	static private string Refused(string action, string reason)
		=> $"Action refused: {action} requires user approval, but {reason}. Nothing was executed or changed.";
}
