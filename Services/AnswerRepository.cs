using System.Text.RegularExpressions;


public class AnswerRepository
{
    private readonly Dictionary<(string field, string level), string> answers = new()
    {
        // What is IPPT?
        { ("ippt", "what"), "IPPT stands for Individual Physical Proficiency Test, which is a mandatory physical fitness test for servicemen in Singapore, designed to assess their basic components of physical fitness and motor skills. It consists of sit-ups, push-ups, and a 2.4 km run, with standards based on age, gender, and vocation." },

        // Why is IPPT important?
        { ("ippt", "why"), "IPPT ensures servicemen maintain baseline fitness, which is essential for operational readiness, emergency response, and endurance during demanding duties. It also promotes discipline, resilience, and healthy living." },

        // PUSH-UP (shared by chest & triceps)
        { ("push-up", "beginner"), @"Push-Up Training Program – Beginner (0-20 reps) 1. Wall Push-Ups: 3x10-12. 2. Incline Push-Ups: 3x8-10. 3. Negative Push-Ups: 2-3x5 (slow descent). 4. Knee Push-Ups: 3x6-8. 3-4 times/week."+"\n\nProgress by increasing reps or moving to harder variations." },
        { ("push-up", "amateur"), @"Push-Up Training Program – Amateur (20-40 reps) 1. Standard Push-Ups: 3-4 sets of max reps (stop 1-2 reps before failure). 2. Wide Push-Ups (chest): 3x10-12. 3. Diamond Push-Ups (triceps): 3x8-10."+"\n\nProgress every 1-2 weeks." },
        { ("push-up", "advanced"), @"Push-Up Training Program – Advanced (40+ reps) 1. Incline Clapping Push-Ups: 2-3x5-8. 2. Archer or One-Arm Push-Ups for challenge."+"\n\n3-5 times/week focusing on form and control." },

        // SIT-UP (shared by abs)
        { ("sit-up", "beginner"), @"Sit-Up Training Program – Beginner (0-15 reps) 1. Assisted Sit-Ups: 3x8-10. 2. Crunches: 3x12-15. 3. Negative Sit-Ups: 2-3x6-8 (slow descent). 4. Plank Holds: 2-3x20-30s. 3-4 times/week."+"\n\nBuild core strength before progressing." },
        { ("sit-up", "amateur"), @"Sit-Up Training Program – Amateur (15-30 reps) 1. Standard Sit-Ups: 3x10-15. 2. Weighted Sit-Ups (use backpack): 3x8-12. 3. Bicycle Crunches: 3x12-15/side. 4. Hanging Knee Tucks: 3x8-10."+"\n\n3-5 times/week." },
        { ("sit-up", "advanced"), @"Sit-Up Training Program – Advanced (30+ reps) 1. Weighted Sit-Ups: 4x12-15. 2. Decline Sit-Ups: 3-4x8-12. 3. Hanging Leg Raises: 3-4x8-12. 4. V-Ups: 3x10-15. 5. Resistance Band Sit-Ups: 3x12-15."+"\n\n3-5 times/week to sustain performance." },

        // RUNNING
        { ("running", "beginner"), @"Running Beginner Plan: 1. Run-Walk Intervals: 1min run + 2min walk for 20-30min, 3-4x/week. 2. LISS Runs: 20-30min steady pace."+"\n\nTips: Maintain upright posture, land mid-foot, increase mileage max 10%/week." },
        { ("running", "amateur"), @"Running Amateur Plan: 1. Tempo Runs: 15-20min 'comfortably hard'. 2. Intervals: 6x200m sprints with 1-2min rest. 3. Long Slow Runs: 40-60min weekly."+"\n\nAdd strides (4-6x60-80m) for turnover. Run 4-5x/week." },
        { ("running", "advanced"), @"Running Advanced Plan: 1. Ladder Intervals (e.g., 400-800-1200m) at 85-95% effort. 2. Progression Long Run: 60-80min, finish at tempo pace. 3. Fartlek: 30-50min with random bursts. 4. Hill Repeats: 8-10x50-100m hill sprints. 5-6x/week, plus recovery work." },

        // General Tips
        { ("push-up", "tips"), @"Push-Up Tips: Keep body straight, engage core, track progress weekly, and stay consistent. Increase difficulty over time by adjusting reps or trying advanced variations." },
        { ("sit-up", "tips"), @"Sit-Up & Core Tips: Keep movements controlled, breathe properly, and vary exercises to engage all core muscles. Practice 3-5x/week for steady improvement." },
        { ("running", "tips"), @"Running Tips: Warm up properly, maintain steady breathing, hydrate before/after runs, and gradually increase mileage. Include dynamic stretches pre-run and static stretches post-run." },

        // General fallback tips prompt
        { ("general", "tips"), @"I can help with training plans! Please share your current level: For Push-Ups & Sit-Ups - Beginner (0-20 reps), Amateur (20-40 reps), Advanced (40+ reps). For Running 2.4km - Beginner (14+ min), Amateur (11-13:59), Advanced (<11:00). Let me know so I can tailor your program!" },

        // NEW: Muscle groups targeted
        { ("push-up", "muscle"),
            @"Push-Up – Targeted Body Parts
            - Primary: Chest (pectoralis major), triceps, front shoulders (anterior deltoids).
            - Secondary: Core muscles (abs, obliques) for stabilization, and serratus anterior." },

        { ("sit-up", "muscle"),
            @"Sit-Up – Targeted Body Parts
            - Primary: Abdominals (rectus abdominis), hip flexors (iliopsoas).
            - Secondary: Obliques during twisting variations." },

        { ("running", "muscle"),
            @"Running – Targeted Body Parts
            - Primary: Quadriceps, hamstrings, calves, glutes.
            - Secondary: Core muscles for stability, hip flexors, and even upper body (arm swing aids efficiency)." },
    };

    public string? GetAnswer(string field, string level)
    {
        if (field == null) return null;

        // Map shared fields
        field = field switch
        {
            "chest" or "triceps" => "push-up",
            "abs" => "sit-up",
            "hip" => "sit-up",
            _ => field
        };

        answers.TryGetValue((field, level), out var answer);

        if (answer == null) return null;

        // (?<=\s|^): A lookbehind assertion that checks if the match is preceded by either:
        // \s – a whitespace character (space, tab, etc.)
        // ^ – the start of the string
        // (\d+\.): Matches one or more digits (\d+) followed by a literal dot (.). This part is captured for replacement.
        // "\n$1": Replaces the matched number-dot combo by inserting a newline before it.//
        // $1 refers to the captured group (\d+\.), i.e., the actual numbered item.//
        answer = Regex.Replace(answer, @"(?<=\s|^)(\d+\.)\s", "\n$1");

        return answer.Trim();
    }

    public static string HandleIPPTCheck(string gender, string ageStr, string pushupStr, string situpStr, string runtimeStr)
    {
        string? genderNormalized = gender?.Trim().ToLower();
        if (genderNormalized == "m") genderNormalized = "male";
        else if (genderNormalized == "f") genderNormalized = "female";

        if (genderNormalized != "male" && genderNormalized != "female")
            return "Invalid gender. Please enter 'male' or 'female' (or 'm'/'f').";

        if (!int.TryParse(ageStr, out int age) || age < 18 || age > 45)
            return "Invalid age. IPPT scoring is for ages 18-45.";

        if (!int.TryParse(pushupStr, out int pushups) || pushups < 0)
            return "Invalid push-up count.";

        if (!int.TryParse(situpStr, out int situps) || situps < 0)
            return "Invalid sit-up count.";

        int ageCat = DetermineAgeCategory(age);
        if (ageCat == -1) return "Sorry, scoring only available for ages 18-45.";

        return ComputeIPPTScore(genderNormalized, ageCat, situps, pushups, runtimeStr);
    }

    private static int DetermineAgeCategory(int age)
    {
        if (age < 18 || age > 45) return -1;
        if (age < 22) return 1;
        if (age <= 24) return 2;
        if (age <= 27) return 3;
        if (age <= 30) return 4;
        if (age <= 33) return 5;
        if (age <= 36) return 6;
        if (age <= 39) return 7;
        if (age <= 42) return 8;
        return 9;
    }

    private static string ComputeIPPTScore(string gender, int ageGroup, int sitUps, int pushUps, string runTime)
    {

        int sitScore = IPPTScorer.GetSitUpScore(gender, sitUps, ageGroup);
        int pushScore = IPPTScorer.GetPushUpScore(gender, pushUps, ageGroup);
        int runScore = IPPTScorer.GetRunScore(gender, runTime, ageGroup);

        int total = sitScore + pushScore + runScore;

        string result;
        if (total >= 85) result = $"🎉 Congrats! You scored {total} — Gold!";
        else if (total >= 75) result = $"👏 Well done! You scored {total} — Silver!";
        else if (total >= 61) result = $"👍 You scored {total} — Pass!";
        else result = $"😔 You scored {total} — Not a pass. Keep training!";

        return $"Sit-up score: {sitScore}, Push-up score: {pushScore}, Run score: {runScore} → Total: {total}. {result}";
    }

    public static string CalculateRequiredForTarget(string gender, int age, string target,
    int? pushups, int? situps, string runtime)
    {
        int ageGroup = DetermineAgeCategory(age);
        if (ageGroup == -1)
            return "⚠️ Invalid age for IPPT categories.";

        int? scorePush = pushups.HasValue ? IPPTScorer.GetPushUpScore(gender, pushups.Value, ageGroup) : null;
        int? scoreSit = situps.HasValue ? IPPTScorer.GetSitUpScore(gender, situps.Value, ageGroup) : null;
        int? scoreRun = !string.IsNullOrEmpty(runtime) ? IPPTScorer.GetRunScore(gender, runtime, ageGroup) : null;

        Console.WriteLine($"[DEBUG] Age: {age}, Age Group: {ageGroup}");
        Console.WriteLine($"[DEBUG] Push-Up Score: {scorePush}");
        Console.WriteLine($"[DEBUG] Sit-Up Score: {scoreSit}");
        Console.WriteLine($"[DEBUG] Run Score: {scoreRun}");

        var stationScores = new Dictionary<string, int?>()
        {
            { "push-up", scorePush },
            { "sit-up", scoreSit },
            { "runtime", scoreRun }
        };

        var known = stationScores.Where(kv => kv.Value.HasValue).ToDictionary(kv => kv.Key, kv => kv.Value.Value);
        var unknown = stationScores.FirstOrDefault(kv => !kv.Value.HasValue).Key;

        if (unknown == null)
            return " All station scores are already provided. No need for reverse calculation.";

        int requiredTotal = target.ToLower() switch
        {
            "gold" => 85,
            "silver" => 75,
            _ => 61 // pass
        };

        int minPerStation = target.ToLower() switch
        {
            "gold" => 21,
            "silver" => 15,
            _ => 1
        };

        int currentTotal = known.Values.Sum();
        int needed = requiredTotal - currentTotal;

        // Cap needed to max 25 and min required
        if (needed > 25)
        {
            return $"❌ Based on your current scores, reaching {target.ToUpper()} is not possible.\n" +
                   $"You need {needed} points in '{unknown}', but max per station is 25.";
        }

        if (needed < minPerStation) needed = minPerStation;

        string suggestion = unknown switch
        {
            "push-up" => $"🏋️ You need at least {needed} points for Push-Ups → estimated {ReversePushUpScore(gender, age, needed)} reps.",
            "sit-up" => $"🧍‍♂️ You need at least {needed} points for Sit-Ups → estimated {ReverseSitUpScore(gender, age, needed)} reps.",
            "runtime" => $"🏃 You need at least {needed} points for the 2.4km run → approximately {ReverseRunScore(gender, age, needed)} minutes.",
            _ => "⚠️ Unknown station type."
        };

        return $"🎯 To reach {target.ToUpper()}:\n" +
               $"• Known: {string.Join(", ", known.Select(kv => $"{kv.Key}: {kv.Value} pts"))}\n" +
               $"• Required in '{unknown}': {needed} pts\n\n{suggestion}";
    }

    public static int ReversePushUpScore(string gender, int ageGroup, int targetScore)
    {
        for (int reps = 0; reps <= 80; reps++)
        {
            if (IPPTScorer.GetPushUpScore(gender, reps, ageGroup) >= targetScore)
                return reps;
        }
        return -1;
    }

    public static int ReverseSitUpScore(string gender, int ageGroup, int targetScore)
    {
        for (int reps = 0; reps <= 80; reps++)
        {
            if (IPPTScorer.GetSitUpScore(gender, reps, ageGroup) >= targetScore)
                return reps;
        }
        return -1;
    }

    public static string ReverseRunScore(string gender, int ageGroup, int targetScore)
    {
        for (int min = 6; min <= 20; min++)
        {
            for (int sec = 0; sec < 60; sec++)
            {
                string runtime = $"{min}:{sec:D2}";
                if (IPPTScorer.GetRunScore(gender, runtime, ageGroup) >= targetScore)
                    return runtime;
            }
        }
        return "unreachable";
    }
}
