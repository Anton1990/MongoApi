using Microsoft.AspNetCore.Mvc;

namespace MongoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StressController : ControllerBase
{
    private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "stress-files");

    /// <summary>
    /// Создаёт `count` файлов параллельно. Каждый файл пишет значения 0..iterations каждые delayMs мс.
    /// Запускается в фоне — возвращает сразу.
    /// </summary>
    [HttpPost("files")]
    public IActionResult StartFileStress(
        [FromQuery] int count = 10,
        [FromQuery] int iterations = 10000,
        [FromQuery] int delayMs = 50)
    {
        if (count < 1 || count > 1000) return BadRequest("count must be 1-1000");
        if (iterations < 1 || iterations > 100_000) return BadRequest("iterations must be 1-100000");
        if (delayMs < 1 || delayMs > 5000) return BadRequest("delayMs must be 1-5000");

        Directory.CreateDirectory(TempDir);

        var jobId = Guid.NewGuid().ToString("N")[..8];

        // Fire-and-forget — не блокируем запрос
        _ = Task.Run(async () =>
        {
            var tasks = Enumerable.Range(0, count).Select(_ => WriteFileAsync(iterations, delayMs));
            await Task.WhenAll(tasks);
        });

        return Accepted(new
        {
            jobId,
            files = count,
            iterations,
            delayMs,
            estimatedDurationSec = (long)iterations * delayMs / 1000,
            outputDir = TempDir
        });
    }

    /// <summary>
    /// Удаляет все созданные тестовые файлы.
    /// </summary>
    [HttpDelete("files")]
    public IActionResult CleanupFiles()
    {
        if (!Directory.Exists(TempDir))
            return Ok(new { deleted = 0 });

        var files = Directory.GetFiles(TempDir);
        foreach (var f in files)
            System.IO.File.Delete(f);

        return Ok(new { deleted = files.Length });
    }

    /// <summary>
    /// Показывает сколько файлов сейчас в папке и общий размер.
    /// </summary>
    [HttpGet("files/status")]
    public IActionResult GetStatus()
    {
        if (!Directory.Exists(TempDir))
            return Ok(new { fileCount = 0, totalSizeKb = 0 });

        var files = Directory.GetFiles(TempDir);
        var totalBytes = files.Sum(f => new FileInfo(f).Length);

        return Ok(new
        {
            fileCount = files.Length,
            totalSizeKb = totalBytes / 1024
        });
    }

    private static async Task WriteFileAsync(int iterations, int delayMs)
    {
        var path = Path.Combine(TempDir, $"{Guid.NewGuid()}.txt");
        await using var writer = new StreamWriter(path, append: false);

        for (var i = 0; i <= iterations; i++)
        {
            await writer.WriteLineAsync(i.ToString());
            await writer.FlushAsync();
            await Task.Delay(delayMs);
        }
    }
}
