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
        {"ippt", "ippt"}
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

    public string DetectIntent(string message)
    {
        message = message.ToLower();

        // IPPT intent detection
        if (Regex.IsMatch(message, @"\b(ippt|score|result|performance)\b"))
        {
            return "IPPT_CHECK";
        }

        // Fallback: check question intents
        string questionType = DetectQuestionIntent(message);
        if (questionType != null)
        {
            return "QUESTION";
        }

        // Unknown if nothing else matched
        return "UNKNOWN";

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
        message = message.ToLower();

        string field = DetectField(message);
        string level = DetectLevel(message);
        string questionType = DetectQuestionIntent(message);
        string gender = DetectGender(message);
        string MiscIntent = DetectMiscIntent(message); 

        return new IntentResult
        {
            Field = DetectField(message),
            Level = DetectLevel(message),
            QuestionType = DetectQuestionIntent(message),
            Gender = DetectGender(message),
            MiscIntent = DetectMiscIntent(message) 
        };
    }

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
