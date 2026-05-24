using ResumeReview.SampleGenerator;

var templatePath = "../../template/resumetemplate.dotx";
var outputDir = "../output";
int count = 18;
int? seed = null;

for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--template" && i + 1 < args.Length) templatePath = args[++i];
    else if (args[i] == "--out" && i + 1 < args.Length) outputDir = args[++i];
    else if (args[i] == "--count" && i + 1 < args.Length) count = int.Parse(args[++i]);
    else if (args[i] == "--seed" && i + 1 < args.Length) seed = int.Parse(args[++i]);
}

Directory.CreateDirectory(outputDir);

var generator = new ResumeGenerator(templatePath, seed);
var profiles = CandidateCatalog.BuildDiverseCohort(count, seed);

foreach (var profile in profiles)
{
    var safeName = string.Concat(profile.FullName.Where(c => char.IsLetterOrDigit(c) || c == ' '))
                          .Replace(' ', '_').ToLowerInvariant();
    var outPath = Path.Combine(outputDir, $"{safeName}.docx");
    generator.GenerateTo(profile, outPath);
    Console.WriteLine($"Generated {outPath}");
}

Console.WriteLine($"Done. Wrote {profiles.Count} resume(s) to {Path.GetFullPath(outputDir)}");
