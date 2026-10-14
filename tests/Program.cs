var testRoot = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "../../../../work/test-temp")
);
var dir = Path.GetFullPath(
    Path.Combine(testRoot, "ct-localization-test-" + Guid.NewGuid().ToString("N"))
);
if (!dir.StartsWith(testRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
{
    throw new Exception("Invalid test path");
}

Directory.CreateDirectory(dir);
try
{
    var count = new LocalizationTests(dir).Run();
    Console.WriteLine($"PASS: {count} checks.");
}
finally
{
    Directory.Delete(dir, true);
}
