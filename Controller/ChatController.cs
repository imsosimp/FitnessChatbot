using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Text.RegularExpressions;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly NLUProcessor _nlu;
    private readonly AnswerRepository _repo;

    // Session keys
    private const string SessionFieldKey = "SessionField";
    private const string SessionLevelKey = "SessionLevel";
    private const string SessionIpptFlowKey = "IpptFlow";
    private const string SessionIpptAgeKey = "IpptAge";
    private const string SessionIpptPushupsKey = "IpptPushups";
    private const string SessionIpptSitupsKey = "IpptSitups";
    private const string SessionIpptRuntimeKey = "IpptRuntime";
    private const string SessionIpptGenderKey = "IpptGender";

    private readonly string[] NegativeIntentKeywords = {
        "slow", "avoid", "hate", "dislike", "don't want", "dont want", "not interested", "skip", "fail"
    };
    private readonly string[] ImproveIntentKeywords = {
        "improve", "increase", "boost", "faster", "better", "enhance", "lower", "reduce time", "quicker", "progress"
    };

    public ChatController(NLUProcessor nlu, AnswerRepository repo)
    {
        _nlu = nlu;
        _repo = repo;
    }

    [HttpPost]
    public IActionResult Post([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return Ok(new ChatResponse { Response = "Sorry, I couldn't understand your question." });
        }

        string message = request.Message.ToLower().Trim();

        if (Regex.IsMatch(message, @"\bwhy\s+is\s+ippt\b") ||
            Regex.IsMatch(message, @"\bwhat\s+is\s+ippt\b"))
            {
                var answer = _repo.GetAnswer("ippt", message.Contains("why") ? "why" : "what");
                return Ok(new ChatResponse { Response = answer });
            }

        var ipptFlow = HttpContext.Session.GetString(SessionIpptFlowKey);
        if (ipptFlow == "true")
        {
            return HandleIpptFlow(message);
        }
    
        var intent = _nlu.Classify(message);

        switch (intent.MiscIntent)
        {
            case "greeting":
                return Ok(new ChatResponse { Response = "Hi there 👋 How can I help you today?" });

            case "thanks":
                return Ok(new ChatResponse { Response = "You’re welcome! Let me know if I can help with anything else." });

            case "farewell":
                ClearSession();
                ClearIpptSession();
                return Ok(new ChatResponse { Response = "Good‑bye 👋 Stay active and take care!", EndChat = true });
        }

        if (intent.QuestionType == "ippt_check" || IsIPPTCheckRequest(message))
        {
            HttpContext.Session.SetString(SessionIpptFlowKey, "true");
            return Ok(new ChatResponse { Response = "Please specify your gender as 'male' or 'female' (you can also enter 'm' or 'f')." });
        }

        // Normalize and map fields
        string normalizedField = NormalizeField(intent.Field);
        if (normalizedField == "bodypart" && !string.IsNullOrEmpty(message))
        {
            var mappedExercise = _nlu.MapBodypartToExercise(message);
            if (!string.IsNullOrEmpty(mappedExercise))
            {
                normalizedField = mappedExercise;
            }
            else
            {
                normalizedField = intent.Field;
            }
        }

        var prevField = HttpContext.Session.GetString(SessionFieldKey);
        var prevLevel = HttpContext.Session.GetString(SessionLevelKey);

        if (HasNegativeIntent(message) && !HasImproveIntent(message))
        {
            return Ok(new ChatResponse
            {
                Response = "It looks like you're not looking to train right now. Let me know if you have any other questions!"
            });
        }

        if (!string.IsNullOrEmpty(normalizedField))
        {
            HttpContext.Session.SetString(SessionFieldKey, normalizedField);
            prevField = normalizedField;
        }

        if (!string.IsNullOrEmpty(intent.Level))
        {
            HttpContext.Session.SetString(SessionLevelKey, intent.Level);
            prevLevel = intent.Level;
        }

        if (!string.IsNullOrEmpty(intent.QuestionType) && intent.QuestionType == "tips")
        {
            // Figure out which exercise/bodypart the user mentioned
            var fieldToUse = normalizedField ?? _nlu.DetectField(message);

            var tipsAnswer = _repo.GetAnswer(fieldToUse, "tips");
            if (!string.IsNullOrEmpty(tipsAnswer))
                return Ok(new ChatResponse { Response = tipsAnswer });
        }

        if (intent.Field == "tips" || prevField == "tips")
        {
            var tipsAnswer = _repo.GetAnswer(prevField, "tips");
            if (!string.IsNullOrEmpty(tipsAnswer))
            {
                ClearSession();
                return Ok(new ChatResponse { Response = tipsAnswer });
            }
            return Ok(new ChatResponse { Response = "Sorry, I couldn't find any tips for that topic." });
        }

        Console.WriteLine($"[ChatController] intent.Field={intent.Field}, normalizedField={normalizedField}, prevField={prevField}, prevLevel={prevLevel}");
        if (!string.IsNullOrEmpty(prevField) && string.IsNullOrEmpty(prevLevel))
        {
            if (HasImproveIntent(message))
            {
                // User wants to improve but hasn't given level → prompt for level
                string levelPrompt = prevField switch
                {
                    "push-up" or "sit-up" =>
                        "To help you improve, please tell me your current level:\n" +
                        "- Beginner: 0-20 reps in one minute\n" +
                        "- Amateur: 20-40 reps in one minute\n" +
                        "- Advanced: 40+ reps in one minute",
                    "running" =>
                        "To help you improve, please tell me your current level:\n" +
                        "- Beginner: 2.4km in 14 minutes or more\n" +
                        "- Amateur: 13:59 to 11:00\n" +
                        "- Advanced: 10:59 or faster",
                    _ =>
                        $"To help you improve your {prevField}, please tell me your level:\n- Beginner\n- Amateur\n- Advanced"
                };

                return Ok(new ChatResponse { Response = levelPrompt });
            }
            else
            {
                // No improve intent → generic training plan prompt
                string levelPrompt = prevField switch
                {
                    "push-up" or "sit-up" =>
                        "To provide the best training plan, please tell me your current level:\n" +
                        "- Beginner: 0-20 reps in one minute\n" +
                        "- Amateur: 20-40 reps in one minute\n" +
                        "- Advanced: 40+ reps in one minute",
                    "running" =>
                        "To provide the best training plan, please tell me your current level:\n" +
                        "- Beginner: 2.4km in 14 minutes or more\n" +
                        "- Amateur: 13:59 to 11:00\n" +
                        "- Advanced: 10:59 or faster",
                    _ =>
                        $"To provide the best training plan for your {prevField}, please tell me your level:\n- Beginner\n- Amateur\n- Advanced"
                };

                return Ok(new ChatResponse { Response = levelPrompt });
            }
        }

        if (!string.IsNullOrEmpty(intent.QuestionType))
        {
            var fieldToUse = normalizedField ?? intent.Field;

            // Specifically handle questions about muscles/body parts
            if (!string.IsNullOrEmpty(fieldToUse) && 
                (intent.QuestionType == "what" || intent.QuestionType == "muscle"))
            {
                var targetAnswer = _repo.GetAnswer(fieldToUse, "muscle");
                if (!string.IsNullOrEmpty(targetAnswer))
                {
                    return Ok(new ChatResponse { Response = targetAnswer });
                }
            }

            // General question intent fallback
            var generalAnswer = _repo.GetAnswer(fieldToUse, intent.QuestionType);
            if (!string.IsNullOrEmpty(generalAnswer))
            {
                return Ok(new ChatResponse { Response = generalAnswer });
            }
        }

        if (!string.IsNullOrEmpty(prevField) && !string.IsNullOrEmpty(prevLevel))
        {
            var answer = _repo.GetAnswer(prevField, prevLevel);
            if (!string.IsNullOrEmpty(answer))
            {
                ClearSession();
                return Ok(new ChatResponse { Response = answer });
            }

            return Ok(new ChatResponse
            {
                Response = $"I recognized your level as '{prevLevel}', but couldn't find a training plan for '{prevField}'. Please check the training area or try rephrasing."
            });
        }

        return Ok(new ChatResponse
        {
            Response = "Sorry, I couldn't process your request. Could you please rephrase or be more specific?"
        });
    }

    private IActionResult HandleIpptFlow(string message)
    {
        var gender = HttpContext.Session.GetString(SessionIpptGenderKey);
        var age = HttpContext.Session.GetString(SessionIpptAgeKey);
        var pushups = HttpContext.Session.GetString(SessionIpptPushupsKey);
        var situps = HttpContext.Session.GetString(SessionIpptSitupsKey);
        var runtime = HttpContext.Session.GetString(SessionIpptRuntimeKey);

        if (string.IsNullOrEmpty(gender))
        {
            string lowerMsg = message.Trim().ToLower();
            if (lowerMsg == "m" || lowerMsg == "male")
            {
                HttpContext.Session.SetString(SessionIpptGenderKey, "male");
                return Ok(new ChatResponse { Response = "Enter your age (e.g., 25)." });
            }
            else if (lowerMsg == "f" || lowerMsg == "female")
            {
                HttpContext.Session.SetString(SessionIpptGenderKey, "female");
                return Ok(new ChatResponse { Response = "Enter your age (e.g., 25)." });
            }
            else
            {
                return Ok(new ChatResponse { Response = "Invalid gender. Please specify 'male' or 'female' (or 'm'/'f')." });
            }
        }

        if (string.IsNullOrEmpty(age))
        {
            if (int.TryParse(message, out int userAge) && userAge > 0)
            {
                HttpContext.Session.SetString(SessionIpptAgeKey, userAge.ToString());
                return Ok(new ChatResponse { Response = "Enter your number of push-ups in one minute." });
            }
            else
            {
                return Ok(new ChatResponse { Response = "Invalid age. Please enter a number (e.g., 25)." });
            }
        }

        if (string.IsNullOrEmpty(pushups))
        {
            if (int.TryParse(message, out int push) && push >= 0)
            {
                HttpContext.Session.SetString(SessionIpptPushupsKey, push.ToString());
                return Ok(new ChatResponse { Response = "Enter your number of sit-ups in one minute." });
            }
            else
            {
                return Ok(new ChatResponse { Response = "Invalid input. Please enter the number of push-ups as a whole number." });
            }
        }

        if (string.IsNullOrEmpty(situps))
        {
            if (int.TryParse(message, out int sit) && sit >= 0)
            {
                HttpContext.Session.SetString(SessionIpptSitupsKey, sit.ToString());
                return Ok(new ChatResponse { Response = "Enter your 2.4km run time (e.g., 11:30)." });
            }
            else
            {
                return Ok(new ChatResponse { Response = "Invalid input. Please enter the number of sit-ups as a whole number." });
            }
        }

        if (string.IsNullOrEmpty(runtime))
        {
            if (Regex.IsMatch(message, @"^\d{1,2}:\d{2}$"))
            {
                var parts = message.Split(':');
                if (int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int seconds) && seconds < 60)
                {
                    string runTimeDecimal = $"{minutes}.{seconds:D2}";
                    HttpContext.Session.SetString(SessionIpptRuntimeKey, runTimeDecimal);

                    var result = AnswerRepository.HandleIPPTCheck(
                        HttpContext.Session.GetString(SessionIpptGenderKey),
                        HttpContext.Session.GetString(SessionIpptAgeKey),
                        HttpContext.Session.GetString(SessionIpptPushupsKey),
                        HttpContext.Session.GetString(SessionIpptSitupsKey),
                        runTimeDecimal);

                    ClearIpptSession();
                    return Ok(new ChatResponse { Response = result });
                }
            }

            var retriesStr = HttpContext.Session.GetString("IpptRuntimeRetries") ?? "0";
            int retries = int.TryParse(retriesStr, out var r) ? r : 0;
            retries++;
            HttpContext.Session.SetString("IpptRuntimeRetries", retries.ToString());

            if (retries >= 5)
            {
                ClearIpptSession();
                return Ok(new ChatResponse { Response = "Too many invalid attempts. IPPT check cancelled. Please start over if you wish." });
            }

            return Ok(new ChatResponse
            {
                Response = "Invalid runtime format. Please enter it as minutes:seconds (e.g., 11:30)."
            });
        }

        ClearIpptSession();
        return Ok(new ChatResponse { Response = "Something went wrong with IPPT check. Please start over." });
    }

    private string NormalizeField(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.ToLower().Trim();

        return raw switch
        {
            var f when f.Contains("push") => "push-up",
            var f when f.Contains("sit") || f.Contains("abs") || f.Contains("core") => "sit-up",
            var f when f.Contains("run") || f.Contains("jog") || f.Contains("2.4") => "running",
            _ => raw
        };
    }

private bool IsIPPTCheckRequest(string message)
{
    if (string.IsNullOrEmpty(message)) return false;

    // Quick checks for common phrasing
    if (message.Contains("ippt check", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("check ippt", StringComparison.OrdinalIgnoreCase) ||
        (message.Contains("ippt", StringComparison.OrdinalIgnoreCase) &&
        message.Contains("result", StringComparison.OrdinalIgnoreCase)))
    {
        return true;
    }

    // General regex to match any keywords like ippt, score, result, performance
    return Regex.IsMatch(message, @"\b(ippt|score|result|performance)\b", RegexOptions.IgnoreCase);
}

    private void ClearSession()
    {
        HttpContext.Session.Remove(SessionFieldKey);
        HttpContext.Session.Remove(SessionLevelKey);
    }

    private void ClearIpptSession()
    {
        HttpContext.Session.Remove(SessionIpptFlowKey);
        HttpContext.Session.Remove(SessionIpptGenderKey);
        HttpContext.Session.Remove(SessionIpptAgeKey);
        HttpContext.Session.Remove(SessionIpptPushupsKey);
        HttpContext.Session.Remove(SessionIpptSitupsKey);
        HttpContext.Session.Remove(SessionIpptRuntimeKey);
        HttpContext.Session.Remove("IpptRuntimeRetries");
    }

    private bool HasNegativeIntent(string message) =>
        NegativeIntentKeywords.Any(k => message.Contains(k));

    private bool HasImproveIntent(string message) =>
        ImproveIntentKeywords.Any(k => message.Contains(k));
}

public class ChatRequest
{
    public string? Message { get; set; }
}

public class ChatResponse
{
    public string? Response { get; set; }
    public bool EndChat { get; set; }
}
