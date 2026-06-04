using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace MongoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StressController : ControllerBase
{
    // Активные jobs: jobId -> CancellationTokenSource
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _jobs = new();

    /// <summary>
    /// Запускает count внутренних тасок, каждая делает тяжёлую работу внутри пода.
    /// Один вызов — вся нагрузка внутри приложения. Компьютер не грузит.
    /// </summary>
    [HttpPost("tasks")]
    public IActionResult StartTasks(
        [FromQuery] int count = 50,
        [FromQuery] int durationSec = 60)
    {
        if (count < 1 || count > 500) return BadRequest("count must be 1-500");
        if (durationSec < 1 || durationSec > 3600) return BadRequest("durationSec must be 1-3600");

        var jobId = Guid.NewGuid().ToString("N")[..8];
        var cts = new CancellationTokenSource();
        _jobs[jobId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                var tasks = Enumerable.Range(0, count)
                    .Select(i => WorkerTaskAsync(i, durationSec, cts.Token));
                await Task.WhenAll(tasks);
            }
            finally
            {
                _jobs.TryRemove(jobId, out _);
                cts.Dispose();
            }
        });

        return Accepted(new
        {
            jobId,
            tasks = count,
            durationSec,
            stop = $"DELETE /api/stress/tasks/{jobId}"
        });
    }

    /// <summary>
    /// Останавливает конкретный job по jobId.
    /// </summary>
    [HttpDelete("tasks/{jobId}")]
    public IActionResult StopTask(string jobId)
    {
        if (_jobs.TryRemove(jobId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            return Ok(new { jobId, stopped = true });
        }
        return NotFound(new { jobId, stopped = false });
    }

    /// <summary>
    /// Останавливает все активные jobs.
    /// </summary>
    [HttpDelete("tasks")]
    public IActionResult StopAllTasks()
    {
        var ids = _jobs.Keys.ToList();
        foreach (var id in ids)
        {
            if (_jobs.TryRemove(id, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
        return Ok(new { stopped = ids.Count, ids });
    }

    /// <summary>
    /// Показывает активные jobs.
    /// </summary>
    [HttpGet("tasks")]
    public IActionResult GetTasks() =>
        Ok(new { active = _jobs.Count, jobIds = _jobs.Keys });

    // Каждый worker: аллоцирует разные размеры объектов (Gen0/Gen1/Gen2/LOH),
    // держит часть в памяти чтобы объекты доживали до старших поколений.
    private static async Task WorkerTaskAsync(int index, int durationSec, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(durationSec);
        var survivors = new List<byte[]>(); // держим живыми → продвижение в Gen1/Gen2

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            // Gen0: много мелких короткоживущих аллокаций
            for (var i = 0; i < 100; i++)
            {
                var _ = new byte[1024 * (index % 10 + 1)]; // 1–10 KB
            }

            // Gen1/Gen2: объекты которые выживают несколько GC циклов
            survivors.Add(new byte[1024 * 20]); // 20 KB — держим в памяти
            if (survivors.Count > 50)
                survivors.RemoveRange(0, 10); // освобождаем часть → GC их соберёт

            // LOH: каждый 20-й цикл аллоцируем > 85KB → сразу в Gen2
            if (index % 20 == 0)
            {
                var loh = new byte[1024 * 100]; // 100 KB → LOH
                Array.Fill(loh, (byte)42);      // записываем чтобы не оптимизировалось
            }

            // CPU работа: имитируем вычисления
            var sum = 0L;
            for (var i = 0; i < 50_000; i++) sum += i;

            await Task.Delay(10, ct); // 10ms пауза между итерациями
        }
    }

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
