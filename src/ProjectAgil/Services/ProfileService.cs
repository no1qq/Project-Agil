using System.IO;
using ProjectAgil.Helpers;
using ProjectAgil.Models;

namespace ProjectAgil.Services;

public interface IProfileService
{
    OptimizationProfile Active { get; set; }

    void SaveActive();

    IReadOnlyList<OptimizationProfile> LoadAll();

    void Save(OptimizationProfile profile);

    bool Exists(string name);

    void Delete(string name);

    void Export(OptimizationProfile profile, string path);

    OptimizationProfile? Import(string path);
}

public sealed class ProfileService : IProfileService
{
    public ProfileService()
    {
        AppPaths.EnsureCreated();
        Active = JsonStore.Read<OptimizationProfile>(AppPaths.ActiveProfileFile) ?? new OptimizationProfile();
    }

    public OptimizationProfile Active { get; set; }

    public void SaveActive() => JsonStore.Write(AppPaths.ActiveProfileFile, Active);

    public IReadOnlyList<OptimizationProfile> LoadAll()
    {
        var list = new List<OptimizationProfile>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(AppPaths.Profiles, "*.json"))
            {
                var profile = JsonStore.Read<OptimizationProfile>(file);
                if (profile is not null)
                {
                    list.Add(profile);
                }
            }
        }
        catch
        {
        }

        return [.. list.OrderByDescending(p => p.SavedUtc)];
    }

    public void Save(OptimizationProfile profile)
    {
        profile.SavedUtc = DateTime.UtcNow;
        JsonStore.Write(PathFor(profile.Name), profile);
    }

    public bool Exists(string name) => File.Exists(PathFor(name));

    public void Delete(string name)
    {
        try
        {
            var path = PathFor(name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    public void Export(OptimizationProfile profile, string path) => JsonStore.Write(path, profile);

    public OptimizationProfile? Import(string path) => JsonStore.Read<OptimizationProfile>(path);

    private static string PathFor(string name)
    {
        var safe = new string([.. name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)]);
        return Path.Combine(AppPaths.Profiles, $"{safe}.json");
    }
}
