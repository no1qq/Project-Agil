using System.IO;
using ProjectAgil.Helpers;
using ProjectAgil.Models;

namespace ProjectAgil.Services;

public interface IBackupService
{
    IReadOnlyList<BackupSnapshot> LoadAll();

    void Save(BackupSnapshot snapshot);

    void Delete(string id);

    BackupSnapshot? Latest();

    bool HasActiveChanges();
}

public sealed class BackupService : IBackupService
{
    public BackupService() => AppPaths.EnsureCreated();

    public IReadOnlyList<BackupSnapshot> LoadAll()
    {
        var list = new List<BackupSnapshot>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(AppPaths.Backups, "*.json"))
            {
                var snapshot = JsonStore.Read<BackupSnapshot>(file);
                if (snapshot is not null)
                {
                    list.Add(snapshot);
                }
            }
        }
        catch
        {
        }

        return [.. list.OrderByDescending(s => s.CreatedUtc)];
    }

    public void Save(BackupSnapshot snapshot) => JsonStore.Write(PathFor(snapshot.Id), snapshot);

    public void Delete(string id)
    {
        try
        {
            var path = PathFor(id);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    public BackupSnapshot? Latest() => LoadAll().FirstOrDefault(s => !s.Restored);

    public bool HasActiveChanges() => LoadAll().Any(s => !s.Restored && s.Entries.Count > 0);

    private static string PathFor(string id) => Path.Combine(AppPaths.Backups, $"{id}.json");
}
