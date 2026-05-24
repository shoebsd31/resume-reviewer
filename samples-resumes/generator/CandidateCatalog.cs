using Bogus;

namespace ResumeReview.SampleGenerator;

public static class CandidateCatalog
{
    private static readonly string[] InternationalLocales = { "en", "en_GB", "de", "fr", "ja", "es", "pt_BR", "nl" };

    private static readonly (string Title, string Family)[] MlTitles =
    {
        ("Machine Learning Engineer", "ML"),
        ("Applied Scientist", "Research"),
        ("MLOps Engineer", "Platform"),
        ("Research Engineer", "Research"),
        ("Data Scientist", "Data"),
        ("AI Platform Engineer", "Platform"),
        ("LLM Engineer", "ML"),
        ("Computer Vision Engineer", "ML"),
        ("NLP Engineer", "ML"),
        ("ML Tech Lead", "Lead"),
    };

    private static readonly string[] CoreSkills =
    {
        "Python", "PyTorch", "TensorFlow", "Hugging Face", "scikit-learn",
        "Ray", "FastAPI", "Spark", "Kubernetes", "Docker", "AWS", "Azure",
        "GCP", "Terraform", "PostgreSQL", "Redis", "Snowflake", "BigQuery",
        "LLM evaluation", "Vector databases", "MLflow", "Airflow"
    };

    public record SeniorityProfile(string Label, int MinYears, int MaxYears);
    public static readonly SeniorityProfile[] Seniorities =
    {
        new("Junior", 0, 2),
        new("Mid", 3, 5),
        new("Senior", 6, 9),
        new("Staff", 10, 13),
        new("Principal", 14, 22),
    };

    public static IReadOnlyList<CandidateProfile> BuildDiverseCohort(int total, int? seed)
    {
        var fakerSeed = seed ?? Random.Shared.Next();
        var profiles = new List<CandidateProfile>();
        var rng = new Random(fakerSeed);

        // Spread roughly evenly across seniorities.
        var slots = new List<SeniorityProfile>();
        for (var i = 0; i < total; i++)
            slots.Add(Seniorities[i % Seniorities.Length]);

        for (var i = 0; i < total; i++)
        {
            var seniority = slots[i];
            var locale = InternationalLocales[i % InternationalLocales.Length];
            var faker = new Faker(locale) { Random = new Randomizer(fakerSeed + i) };
            var (title, family) = MlTitles[i % MlTitles.Length];
            var isCareerSwitcher = i is 2 or 9; // 2 switchers
            var hasGap = i is 4 or 11;          // 2 with gaps
            var isContractor = i is 6 or 13;    // 2 contractors

            var fullName = faker.Name.FullName();
            var email = faker.Internet.Email(provider: "example.com").ToLowerInvariant();
            var phone = faker.Phone.PhoneNumber("+## ###-###-####");
            var city = faker.Address.City();
            var country = faker.Address.Country();
            var location = $"{city}, {country}";
            var linkedIn = $"https://www.linkedin.com/in/{Slug(fullName)}";
            var github = $"https://github.com/{Slug(fullName)}";

            var years = rng.Next(seniority.MinYears, seniority.MaxYears + 1);
            var experiences = BuildExperiences(faker, rng, family, years, hasGap, isContractor, isCareerSwitcher);
            var education = BuildEducation(faker, rng, isCareerSwitcher);
            var skills = PickSkills(rng, family);
            var awards = BuildAwards(rng);

            var summary = BuildSummary(title, seniority.Label, family, isCareerSwitcher, isContractor);

            profiles.Add(new CandidateProfile(
                FullName: fullName,
                Email: email,
                Phone: phone,
                Location: location,
                LinkedIn: linkedIn,
                GitHub: github,
                Title: title,
                Summary: summary,
                Skills: skills,
                Experiences: experiences,
                Education: education,
                Awards: awards));
        }

        return profiles;
    }

    private static List<ExperienceProfile> BuildExperiences(
        Faker faker, Random rng, string family, int totalYears, bool hasGap, bool isContractor, bool isCareerSwitcher)
    {
        var roles = new List<ExperienceProfile>();
        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var cursor = now;
        var remaining = totalYears * 12;

        if (isCareerSwitcher)
        {
            // Add an earlier non-ML role to start with.
            var earlierMonths = rng.Next(18, 36);
            var prevStart = cursor.AddMonths(-(remaining + earlierMonths));
            var prevEnd = prevStart.AddMonths(earlierMonths);
            roles.Insert(0, new ExperienceProfile(
                Title: rng.Next(2) == 0 ? "Quantitative Analyst" : "Research Physicist",
                Company: faker.Company.CompanyName(),
                Start: prevStart,
                End: prevEnd,
                Description: "Moved into ML after building internal tooling for predictive modelling."));
        }

        var roleCount = Math.Max(2, totalYears / 3);
        for (var r = 0; r < roleCount && remaining > 0; r++)
        {
            var months = Math.Min(remaining, rng.Next(12, 40));
            var end = cursor;
            var start = end.AddMonths(-months);
            var current = r == 0;
            var title = isContractor ? $"Contract {family} Engineer" : SuggestTitle(family, r, totalYears);
            roles.Insert(0, new ExperienceProfile(
                Title: title,
                Company: faker.Company.CompanyName(),
                Start: start,
                End: current ? null : end,
                Description: faker.Lorem.Sentences(2)));
            cursor = start;
            remaining -= months;

            if (hasGap && r == 0)
            {
                var gap = rng.Next(7, 14);
                cursor = cursor.AddMonths(-gap);
            }
        }
        return roles;
    }

    private static string SuggestTitle(string family, int index, int years)
    {
        var prefix = years switch
        {
            < 3 => "",
            < 7 => "Senior ",
            < 12 => "Staff ",
            _ => "Principal "
        };
        var role = family switch
        {
            "Research" => "Applied Scientist",
            "Platform" => "MLOps Engineer",
            "Data" => "Data Scientist",
            "Lead" => "ML Tech Lead",
            _ => "ML Engineer"
        };
        return prefix + role;
    }

    private static List<EducationProfile> BuildEducation(Faker faker, Random rng, bool isCareerSwitcher)
    {
        var institutions = new[] { "ETH Zurich", "University of Tokyo", "TU Munich", "École Polytechnique",
                                   "Carnegie Mellon University", "Indian Institute of Science", "USP São Paulo",
                                   "Delft University", "MILA", "Imperial College London" };
        var field = isCareerSwitcher ? (rng.Next(2) == 0 ? "Physics" : "Mathematical Finance") : "Computer Science";
        var inst = institutions[rng.Next(institutions.Length)];
        var year = DateTime.UtcNow.Year - rng.Next(2, 20);
        var entries = new List<EducationProfile>
        {
            new(inst, "BSc", field, year)
        };
        if (rng.NextDouble() < 0.5)
        {
            entries.Add(new EducationProfile(institutions[rng.Next(institutions.Length)],
                "MSc", isCareerSwitcher ? "Machine Learning" : "Artificial Intelligence", year + rng.Next(1, 4)));
        }
        return entries;
    }

    private static List<string> PickSkills(Random rng, string family)
    {
        var pool = CoreSkills.ToList();
        // Bias by family
        var prefix = family switch
        {
            "Research" => new[] { "PyTorch", "JAX", "Diffusion Models", "Reinforcement Learning" },
            "Platform" => new[] { "Kubernetes", "Terraform", "AWS", "Airflow" },
            "Data" => new[] { "Spark", "Snowflake", "PostgreSQL", "dbt" },
            "Lead" => new[] { "Mentorship", "System design", "Cross-functional collaboration" },
            _ => new[] { "Python", "PyTorch", "Hugging Face", "FastAPI" }
        };
        var picked = new HashSet<string>(prefix);
        while (picked.Count < 10)
            picked.Add(pool[rng.Next(pool.Count)]);
        return picked.OrderBy(_ => rng.Next()).ToList();
    }

    private static List<string> BuildAwards(Random rng)
    {
        var awards = new[]
        {
            "Best paper, ML4H 2024",
            "AWS Certified Machine Learning – Specialty",
            "Speaker, NeurIPS workshop 2023",
            "Google Cloud Professional Data Engineer",
            "Kaggle Competitions Expert"
        };
        return awards.OrderBy(_ => rng.Next()).Take(rng.Next(1, 3)).ToList();
    }

    private static string BuildSummary(string title, string seniority, string family, bool isCareerSwitcher, bool isContractor)
    {
        var prefix = isCareerSwitcher ? "Career-switcher with quantitative background, now a "
                   : isContractor ? "Independent contractor specialising as a "
                   : "Hands-on ";
        var body = $"{seniority.ToLowerInvariant()} {title.ToLowerInvariant()} focused on production ML systems.";
        var suffix = family switch
        {
            "Research" => "Bridges research and engineering; comfortable shipping models from paper to production.",
            "Platform" => "Strong opinions on tooling, reliability, and developer experience for ML teams.",
            "Data" => "Deep experience turning messy data into operational insights.",
            "Lead" => "Comfortable mentoring teams and aligning ML investments with business goals.",
            _ => "Comfortable across the stack from training to serving."
        };
        return $"{prefix}{body} {suffix}";
    }

    private static string Slug(string name)
        => System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
}
