using System.Text.RegularExpressions;

public class NLUProcessor
{
    private readonly Dictionary<string, string> FieldKeywords = new()
    {
        // Exercise fields
        {"push-up", "push-up"}, {"pushups", "push-up"}, {"push up", "push-up"},
        {"sit-up", "sit-up"}, {"situp", "sit-up"}, {"situps", "sit-up"},
        {"run", "running"}, {"running", "running"}, {"jog", "running"}, {"2.4", "running"},
        {"runtime", "running"},
        {"abs", "abs"}, {"core", "abs"},
        {"hip", "hip"}, {"hips", "hip"},

        // Bodypart fields (to trigger level prompt)
        {"chest", "bodypart"}, {"triceps", "bodypart"}, {"arms", "bodypart"}, {"glutes", "bodypart"}, {"butt", "bodypart"},

        // General
        {"body", "bodypart"}, {"muscle", "bodypart"}, {"muscles", "bodypart"},
        {"train", "bodypart"}, {"target", "bodypart"},        

        // IPPT
        // {"ippt", "ippt"}
    };

    private readonly Dictionary<string, string> LevelKeywords = new()
    {
        {"beginner", "beginner"}, {"newbie", "beginner"},
        {"amateur", "amateur"}, {"intermediate", "amateur"},
        {"advanced", "advanced"}, {"advance", "advanced"}, {"pro", "advanced"}, {"adv", "advanced"}
    };

    private readonly Dictionary<string, string> QuestionIntents = new()
    {
        {"why", "why"},
        {"what", "what"},
        {"which", "which"},
        { "what is", "what"},
        {"importance", "why"},
        {"important", "why"},
        // Tips
        {"tip", "tips"}, {"tips", "tips"}, {"advice", "tips"},

    };

    private readonly Dictionary<string, string> GenderKeywords = new()
    {
        {"male", "male"}, {"m", "male"},
        {"female", "female"}, {"f", "female"}
    };

    private readonly Dictionary<string, string> MiscIntents = new()
    {
        {"hi", "greeting"}, {"hello", "greeting"}, {"hey", "greeting"},
        {"thank you", "thanks"}, {"thanks", "thanks"}, {"thx", "thanks"},
        {"appreciate", "thanks"}, {"ty", "thanks"},  {"tysm", "thanks"},
        {"tyvm", "thanks"}, {"bye", "farewell"}, {"goodbye", "farewell"},
        {"good‑bye", "farewell"}, {"see you", "farewell"}, {"exit", "farewell"}
    };

    public string? DetectMiscIntent(string message)
    {
        foreach (var pair in MiscIntents)
        {
            if (message.Contains(pair.Key))
                return pair.Value;
        }
        return null;
    }

    public string DetectGender(string message)
    {
        foreach (var pair in GenderKeywords)
        {
            var regex = new Regex($@"\b{Regex.Escape(pair.Key)}\b", RegexOptions.IgnoreCase);
            if (regex.IsMatch(message))
            {
                return pair.Value;
            }
        }
        return null;
    }

    public IntentResult Classify(string message)
    {
        message = message.ToLower().Trim();

        var result = new IntentResult
        {
            Field = DetectField(message),
            Level = DetectLevel(message),
            QuestionType = DetectQuestionIntent(message),
            Gender = DetectGender(message),
            MiscIntent = DetectMiscIntent(message)
        };

        // IPPT Check detection (based on stricter match)
        if (Regex.IsMatch(message, @"\bippt\b") &&
            Regex.IsMatch(message, @"\b(check|score|result|performance)\b"))
        {
            Console.WriteLine("[NLU] Intent matched for IPPT_CHECK with keywords: ippt + check/score/result/performance");
            result.QuestionType = "ippt_check"; // override if needed
        }

        return result;
    }

    // public IntentResult Classify(string message)
    // {
    //     message = message.ToLower();

    //     return new IntentResult
    //     {
    //         Field = DetectField(message),
    //         Level = DetectLevel(message),
    //         QuestionType = DetectQuestionIntent(message),
    //         Gender = DetectGender(message),
    //         MiscIntent = DetectMiscIntent(message)
    //     };
    // }

    // public string DetectIntent(string message)
    // {
    //     message = message.ToLower();

    //     // IPPT intent detection
    //     // if (Regex.IsMatch(message, @"\b(check|score|result|performance)\b"))
    //     // {
    //     //     return "IPPT_CHECK";
    //     // }
    //     if (Regex.IsMatch(message, @"\bippt\b") &&
    //         Regex.IsMatch(message, @"\b(check|score|result|performance)\b"))
    //     {
    //         Console.WriteLine("[NLU] Intent matched for IPPT_CHECK with keywords: ippt + check/score/result/performance");
    //         return "IPPT_CHECK";
    //     }

    //     // Fallback: check question intents
    //     string questionType = DetectQuestionIntent(message);
    //     if (questionType != null)
    //     {
    //         Console.WriteLine($"[NLU] Question-type intent detected: {questionType}");            
    //         return "QUESTION";
    //     }

    //     // Unknown if nothing else matched
    //     return "UNKNOWN";
    // }    

    public bool IsIPPTCheckRequest(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;

        if (message.Contains("ippt check", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[BOT] Matched phrase: 'ippt check'");
            return true;
        }

        if (message.Contains("check ippt", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[BOT] Matched phrase: 'check ippt'");
            return true;
        }

        if (message.Contains("ippt", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("result", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[BOT] Matched combined keywords: 'ippt' and 'result'");
            return true;
        }

        if (Regex.IsMatch(message, @"\b(check|score|result|performance)\b", RegexOptions.IgnoreCase))
        {
            Console.WriteLine("[BOT] Matched general intent keyword: check/score/result/performance");
            return true;
        }

        return false;
    }

    public bool IsReverseIpptQuery(string message)
    {
        Console.WriteLine("[NLU] Checking for reverse IPPT query: " + message);
        message = message.ToLower();

        bool hasResultKeyword = Regex.IsMatch(message, @"\b(pass|silver|gold)\b");
        bool hasIpptKeyword = Regex.IsMatch(message, @"\bippt\b");

        if (hasResultKeyword) Console.WriteLine("[NLU] Matched reverse keyword: pass/silver/gold");
        if (hasIpptKeyword) Console.WriteLine("[NLU] Matched keyword: ippt");

        bool isMatch = hasResultKeyword || hasIpptKeyword;

        if (isMatch) Console.WriteLine("[NLU] Reverse IPPT query detected by IsReverseIpptQuery");

        return isMatch;
    }

    // public bool IsReverseIpptQuery(string message)
    // {
    //     Console.WriteLine("[NLU] Checking for reverse IPPT query: " + message);
    //     return Regex.IsMatch(message, @"\b(need|get|required|how many|how much|how fast|to get|to score|to obtain)\b", RegexOptions.IgnoreCase) &&
    //            Regex.IsMatch(message, @"\b(pass|silver|gold)\b", RegexOptions.IgnoreCase) ||
    //            message.Contains("ippt", StringComparison.OrdinalIgnoreCase);
    // }

    public string DetectField(string message)
    {
        message = message.ToLower();

        // First: exact keyword matches (your existing dictionary)
        foreach (var pair in FieldKeywords)
        {
            var regex = new Regex($@"\b{Regex.Escape(pair.Key)}\b", RegexOptions.IgnoreCase);
            if (regex.IsMatch(message))
            {
                Console.WriteLine($"[NLU] Matched dictionary keyword: '{pair.Key}' → Field: '{pair.Value}'");
                return pair.Value;
            }
        }

        // Fallback regex for push-up variations
        if (Regex.IsMatch(message, @"\bpush[\s\-]?ups?\b", RegexOptions.IgnoreCase))
        {
            Console.WriteLine("[NLU] Matched fallback regex: push-up");
            return "push-up";
        }

        // Fallback regex for sit-up variations
        if (Regex.IsMatch(message, @"\bsit[\s\-]?ups?\b", RegexOptions.IgnoreCase))
        {
            Console.WriteLine("[NLU] Matched fallback regex: sit-up");
            return "sit-up";
        }

        // Fallback regex for running variations
        if (Regex.IsMatch(message, @"\b(run|running|jog|2\.4|runtime)\b", RegexOptions.IgnoreCase))
        {
            Console.WriteLine("[NLU] Matched fallback regex: running");
            return "running";
        }

        return null;
    }

    public string DetectLevel(string message)
    {
        foreach (var pair in LevelKeywords)
        {
            var regex = new Regex($@"\b{Regex.Escape(pair.Key)}\b", RegexOptions.IgnoreCase);
            if (regex.IsMatch(message))
            {
                return pair.Value;
            }
        }
        return null;
    }

    public string DetectQuestionIntent(string message)
    {
        if (message.Contains("muscle") || message.Contains("body part") || message.Contains("muscle group"))
        {
            return "muscle";
        }
        foreach (var pair in QuestionIntents)
        {
            if (message.Contains(pair.Key))
            {
                return pair.Value;
            }
        }
        return null;
    }

    /// Maps a bodypart field to its related exercise keyword, e.g., chest/triceps → push-up, abs → sit-up, etc.
    public string? MapBodypartToExercise(string message)
    {
        message = message.ToLower();

        // Upper body mapping
        if (message.Contains("chest") || message.Contains("tricep") || message.Contains("pec"))
            return "push-up";

        // Core mapping
        if (message.Contains("abs") || message.Contains("core") || message.Contains("stomach") || message.Contains("belly"))
            return "sit-up";

        // Lower body/hips mapping
        if (message.Contains("hip") || message.Contains("glute") || message.Contains("butt"))
            return "hip";

        // Running-related mapping (just in case)
        if (message.Contains("run") || message.Contains("jog") || message.Contains("2.4"))
            return "running";

        return null;
    }    
}

public class IntentResult
{
    public string Field { get; set; }
    public string Level { get; set; }
    public string QuestionType { get; set; }
    public string Gender { get; set; }
    public string MiscIntent { get; set; }
}
