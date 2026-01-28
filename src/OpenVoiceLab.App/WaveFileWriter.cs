using System.Buffers.Binary;

namespace OpenVoiceLab;

public static class WaveFileWriter
{
    public static async Task<string> WritePcmToTempAsync(byte[] pcmData, int sampleRate)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var outputDir = Path.Combine(localAppData, "OpenVoiceLab", "outputs");
        Directory.CreateDirectory(outputDir);
        var filePath = Path.Combine(outputDir, $"stream_{Guid.NewGuid():N}.wav");
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        var header = BuildWaveHeader(pcmData.Length, sampleRate, 1, 16);
        await fs.WriteAsync(header);
        await fs.WriteAsync(pcmData);
        return filePath;
    }

    public static byte[] BuildWaveHeader(int dataLength, int sampleRate, short channels, short bitsPerSample)
    {
        var blockAlign = (short)(channels * (bitsPerSample / 8));
        var byteRate = sampleRate * blockAlign;
        var riffChunkSize = 36 + dataLength;
        var header = new byte[44];
        "RIFF"u8.CopyTo(header.AsSpan(0, 4));
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), riffChunkSize);
        "WAVE"u8.CopyTo(header.AsSpan(8, 4));
        "fmt "u8.CopyTo(header.AsSpan(12, 4));
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16, 4), 16);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(20, 2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(22, 2), channels);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24, 4), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(28, 4), byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(32, 2), blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(34, 2), bitsPerSample);
        "data"u8.CopyTo(header.AsSpan(36, 4));
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(40, 4), dataLength);
        return header;
    }
}
