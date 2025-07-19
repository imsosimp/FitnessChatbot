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
    private const string SessionIpptTargetKey = "IpptTarget"; // gold/silver/pass
    private const string SessionIpptReverseFlowKey = "IpptReverseFlow";
    // private const string SessionIpptKnownStationsKey = "IpptKnownStations"; // pushup,situp or runtime
    private const string SessionGenderKey = "ippt_gender";
    private const string SessionAgeKey = "ippt_age";

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
        Console.WriteLine("[BOT] User Message: " + message);

        if (Regex.IsMatch(message, @"\b(bye|good\s?bye|good‑bye|see\s+you|exit)\b",
                        RegexOptions.IgnoreCase))
        {
            ClearSession();
            ClearIpptSession();
            return Ok(new ChatResponse {
                Response = "Good‑bye 👋 Stay active and take care!",
                EndChat  = true
            });
        }       

        if (Regex.IsMatch(message, @"\bwhy\s+is\s+ippt\b") ||
            Regex.IsMatch(message, @"\bwhat\s+is\s+ippt\b"))
        {
            var answer = _repo.GetAnswer("ippt", message.Contains("why") ? "why" : "what");
            return Ok(new ChatResponse { Response = answer });
        }

        var reverseFlow = HttpContext.Session.GetString(SessionIpptReverseFlowKey);
        if (reverseFlow == "true")
        {
            Console.WriteLine("[BOT] Entering Reverse IPPT Flow...");
            return HandleIpptReverseFlow(message);
        }

        var ipptFlow = HttpContext.Session.GetString(SessionIpptFlowKey);
        if (ipptFlow == "true")
        {
            return HandleIpptFlow(message);
        }

        var intent = _nlu.Classify(message);
        
        if (_nlu.IsReverseIpptQuery(message))
        {
            Console.WriteLine("[BOT] Reverse IPPT query detected.");
            HttpContext.Session.SetString(SessionIpptReverseFlowKey, "true");

            var match = Regex.Match(message, @"\b(pass|silver|gold)\b", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var target = match.Value.ToLower();
                HttpContext.Session.SetString(SessionIpptTargetKey, target);
                Console.WriteLine($"[BOT] Target level set: {target}");
            }

            return Ok(new ChatResponse { Response = "Sure! Let's find out what you need to score that. First, what's your gender, 'male' or 'female' (or 'm'/'f')" });
        }

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

        // General tips
        if (message.Contains("ippt") &&
            Regex.IsMatch(message, @"\b(improve|increase|boost|better|enhance|progress)\b"))
        {
            string generalTips = _repo.GetAnswer("general", "tips");
            return Ok(new ChatResponse { Response = generalTips });
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
            // else
            // {
            //     // Generic training plan prompt
            //     string levelPrompt = prevField switch
            //     {
            //         "push-up" or "sit-up" =>
            //             "To provide the best training plan, please tell me your current level:\n" +
            //             "- Beginner: 0-20 reps in one minute\n" +
            //             "- Amateur: 20-40 reps in one minute\n" +
            //             "- Advanced: 40+ reps in one minute",
            //         "running" =>
            //             "To provide the best training plan, please tell me your current level:\n" +
            //             "- Beginner: 2.4km in 14 minutes or more\n" +
            //             "- Amateur: 13:59 to 11:00\n" +
            //             "- Advanced: 10:59 or faster",
            //         _ =>
            //             $"To provide the best training plan for your {prevField}, please tell me your level:\n- Beginner\n- Amateur\n- Advanced"
            //     };

            //     return Ok(new ChatResponse { Response = levelPrompt });
            // }
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

    private IActionResult? CheckRetries(string key, string cancelMessage)
    {
        // read → increment → store
        int retries = int.TryParse(HttpContext.Session.GetString(key), out var r) ? r : 0;
        retries++;
        HttpContext.Session.SetString(key, retries.ToString());

        if (retries >= 5)
        {
            ClearIpptSession();          
            return Ok(new ChatResponse  
            {
                Response = cancelMessage
            });
        }
        return null;           
    }

    private IActionResult HandleIpptFlow(string message)
    {
        var gender = HttpContext.Session.GetString(SessionIpptGenderKey);
        var age = HttpContext.Session.GetString(SessionIpptAgeKey);
        var pushups = HttpContext.Session.GetString(SessionIpptPushupsKey);
        var situps = HttpContext.Session.GetString(SessionIpptSitupsKey);
        var runtime = HttpContext.Session.GetString(SessionIpptRuntimeKey);

        if (Regex.IsMatch(message, @"\b(bye|good\s?bye|good‑bye|see\s+you|exit)\b",
                        RegexOptions.IgnoreCase))
        {
            ClearSession();
            ClearIpptSession();
            return Ok(new ChatResponse
            {
                Response = "Good‑bye 👋 Stay active and take care!",
                EndChat = true
            });
        }

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
                var cancel = CheckRetries("IpptGenderRetries",
                        "Too many invalid attempts. IPPT check cancelled. Please start over.");
                if (cancel != null) return cancel;

                return Ok(new ChatResponse
                {
                    Response = "Invalid gender. Please specify 'male' or 'female' (or 'm'/'f')."
                });
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
                var cancel = CheckRetries("IpptAgeRetries",
                        "Too many invalid attempts. IPPT check cancelled. Please start over.");
                if (cancel != null) return cancel;

                return Ok(new ChatResponse
                {
                    Response = "Invalid age. Please enter a number (e.g., 25)."
                });
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
                var cancel = CheckRetries("IpptPushupRetries",
                        "Too many invalid attempts. IPPT check cancelled. Please start over.");
                if (cancel != null) return cancel;

                return Ok(new ChatResponse
                {
                    Response = "Invalid input. Please enter the number of push-ups as a whole number."
                });
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
                var cancel = CheckRetries("IpptSitupRetries",
                        "Too many invalid attempts. IPPT check cancelled. Please start over.");
                if (cancel != null) return cancel;

                return Ok(new ChatResponse
                {
                    Response = "Invalid input. Please enter the number of sit-ups as a whole number."
                });
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
        return Regex.IsMatch(message, @"\b(check|score|result|performance)\b", RegexOptions.IgnoreCase);
    }

    private IActionResult HandleIpptReverseFlow(string message)
    {
        var session = HttpContext.Session;

        // Step 1: Gender
        string? gender = session.GetString(SessionGenderKey);
        if (string.IsNullOrEmpty(gender))
        {
            if (Regex.IsMatch(message, @"^(m|male|f|female)$", RegexOptions.IgnoreCase))
            {
                gender = message.ToLower().StartsWith("m") ? "male" : "female";
                session.SetString(SessionGenderKey, gender);
                return Ok(new ChatResponse { Response = "Enter your age (e.g., 25)." });
            }
            return Ok(new ChatResponse { Response = "Please enter your gender as 'male' or 'female' (m/f)." });
        }

        // Step 2: Age
        string? ageStr = session.GetString(SessionAgeKey);
        if (string.IsNullOrEmpty(ageStr))
        {
            if (int.TryParse(message, out int age) && age >= 18 && age <= 45)
            {
                session.SetString(SessionAgeKey, age.ToString());
                session.SetString("ask_pushup", "true");
                return Ok(new ChatResponse { Response = "Do you know your push-up count? (yes/no)" });
            }
            return Ok(new ChatResponse { Response = "Please enter a valid age between 18 and 45." });
        }

        // Auto-trigger when 2 stations are known
        int known = 0;
        if (!string.IsNullOrEmpty(session.GetString("pushup"))) known++;
        if (!string.IsNullOrEmpty(session.GetString("situp"))) known++;
        if (!string.IsNullOrEmpty(session.GetString("runtime"))) known++;

        if (known >= 2)
        {
            return ProceedToReverseScore();
        }

        // Step 3: Push-up
        string? askPush = session.GetString("ask_pushup");
        if (askPush == "true")
        {
            if (Regex.IsMatch(message, @"^(yes|y)$", RegexOptions.IgnoreCase))
            {
                session.SetString("ask_pushup", "awaiting");
                return Ok(new ChatResponse { Response = "Enter your push-up count:" });
            }
            else if (Regex.IsMatch(message, @"^(no|n)$", RegexOptions.IgnoreCase))
            {
                session.SetString("pushup", "");
                session.SetString("ask_pushup", "done");
                session.SetString("ask_situp", "true");
                return Ok(new ChatResponse { Response = "Do you know your sit-up count? (yes/no)" });
            }
            return Ok(new ChatResponse { Response = "Please reply with 'yes' or 'no'." });
        }
        else if (askPush == "awaiting")
        {
            if (int.TryParse(message, out int pushVal) && pushVal >= 0)
            {
                session.SetString("pushup", pushVal.ToString());
                session.SetString("ask_pushup", "done");
                session.SetString("ask_situp", "true");
                return Ok(new ChatResponse { Response = "Do you know your sit-up count? (yes/no)" });
            }
            return Ok(new ChatResponse { Response = "Please enter a valid number for push-ups." });
        }

        // Step 4: Sit-up
        string? askSit = session.GetString("ask_situp");
        if (askSit == "true")
        {
            if (Regex.IsMatch(message, @"^(yes|y)$", RegexOptions.IgnoreCase))
            {
                session.SetString("ask_situp", "awaiting");
                return Ok(new ChatResponse { Response = "Enter your sit-up count:" });
            }
            else if (Regex.IsMatch(message, @"^(no|n)$", RegexOptions.IgnoreCase))
            {
                session.SetString("situp", "");
                session.SetString("ask_situp", "done");
                session.SetString("ask_run", "true");
                return Ok(new ChatResponse { Response = "Do you know your 2.4km run time? (yes/no)" });
            }
            return Ok(new ChatResponse { Response = "Please reply with 'yes' or 'no'." });
        }
        else if (askSit == "awaiting")
        {
            if (int.TryParse(message, out int sitVal) && sitVal >= 0)
            {
                session.SetString("situp", sitVal.ToString());
                session.SetString("ask_situp", "done");
                session.SetString("ask_run", "true");
                return Ok(new ChatResponse { Response = "Do you know your 2.4km run time? (yes/no)" });
            }
            return Ok(new ChatResponse { Response = "Please enter a valid number for sit-ups." });
        }

        // Step 5: Run
        string? askRun = session.GetString("ask_run");
        if (askRun == "true")
        {
            if (Regex.IsMatch(message, @"^(yes|y)$", RegexOptions.IgnoreCase))
            {
                session.SetString("ask_run", "awaiting");
                return Ok(new ChatResponse { Response = "Enter your run time (e.g., 11:30):" });
            }
            else if (Regex.IsMatch(message, @"^(no|n)$", RegexOptions.IgnoreCase))
            {
                session.SetString("runtime", "");
                session.SetString("ask_run", "done");
                return ProceedToReverseScore();
            }
            return Ok(new ChatResponse { Response = "Please reply with 'yes' or 'no'." });
        }
        else if (askRun == "awaiting")
        {
            if (Regex.IsMatch(message, @"^\d{1,2}:\d{2}$"))
            {
                session.SetString("runtime", message);
                session.SetString("ask_run", "done");
                return ProceedToReverseScore();
            }
            return Ok(new ChatResponse { Response = "Please enter time in MM:SS format (e.g., 11:30)." });
        }        

        return Ok(new ChatResponse { Response = "Thanks! Let me know the last station if you know it, or reply 'no' to proceed." });
    }

    private IActionResult ProceedToReverseScore()
    {
        var session = HttpContext.Session;

        string gender = session.GetString(SessionGenderKey) ?? "";
        int age = int.Parse(session.GetString(SessionAgeKey) ?? "0");

        string? pushupStr = session.GetString("pushup");
        string? situpStr = session.GetString("situp");
        string? runtimeStr = session.GetString("runtime");

        int? pushups = int.TryParse(pushupStr, out int p) ? p : null;
        int? situps = int.TryParse(situpStr, out int s) ? s : null;
        string runtime = !string.IsNullOrEmpty(runtimeStr) ? runtimeStr : null;

        string target = session.GetString(SessionIpptTargetKey) ?? "pass";

        string result = AnswerRepository.CalculateRequiredForTarget(
            gender,
            age,
            target,
            pushups,
            situps,
            runtime
        );

        ClearIpptSession(); 

        return Ok(new ChatResponse { Response = result });
    }

    // private int? TryExtractNumber(string input, string keyword)
    // {
    //     var pattern = $@"(\d+)\s*{keyword}";
    //     var match = Regex.Match(input.ToLower(), pattern);
    //     return match.Success ? int.Parse(match.Groups[1].Value) : (int?)null;
    // }

    // private string TryExtractRuntime(string input)
    // {
    //     var match = Regex.Match(input, @"\b(\d{1,2}:\d{2})\b");
    //     return match.Success ? match.Groups[1].Value : null;
    // }


    private void ClearSession()
    {
        HttpContext.Session.Clear();
        // HttpContext.Session.Remove(SessionFieldKey);
        // HttpContext.Session.Remove(SessionLevelKey);
    }

    private void ClearIpptSession()
    {
        // Standard IPPT flow keys
        HttpContext.Session.Remove(SessionIpptFlowKey);
        HttpContext.Session.Remove(SessionIpptGenderKey);
        HttpContext.Session.Remove(SessionIpptAgeKey);
        HttpContext.Session.Remove(SessionIpptPushupsKey);
        HttpContext.Session.Remove(SessionIpptSitupsKey);
        HttpContext.Session.Remove(SessionIpptRuntimeKey);

        // Retry keys
        HttpContext.Session.Remove("IpptRuntimeRetries");
        HttpContext.Session.Remove("IpptGenderRetries");
        HttpContext.Session.Remove("IpptAgeRetries");
        HttpContext.Session.Remove("IpptPushupRetries");
        HttpContext.Session.Remove("IpptSitupRetries");

        // Reverse flow keys
        HttpContext.Session.Remove(SessionIpptReverseFlowKey);
        HttpContext.Session.Remove(SessionIpptTargetKey);
        HttpContext.Session.Remove("pushup");
        HttpContext.Session.Remove("situp");
        HttpContext.Session.Remove("runtime");
        HttpContext.Session.Remove("ask_pushup");
        HttpContext.Session.Remove("ask_situp");
        HttpContext.Session.Remove("ask_run");
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
