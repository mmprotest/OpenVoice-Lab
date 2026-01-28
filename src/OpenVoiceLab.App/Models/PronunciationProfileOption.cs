namespace OpenVoiceLab.Models;

public sealed class PronunciationProfileOption
{
    public string? Id { get; }
    public string Name { get; }

    public PronunciationProfileOption(string? id, string name)
    {
        Id = id;
        Name = name;
    }

    public override string ToString()
    {
        return Name;
    }
}
