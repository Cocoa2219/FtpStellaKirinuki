using System.Text.Json;
using FluentFTP;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Spectre.Console;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace FtpStellaKirinuki;

public class Program
{
    private const string ConfigFile = "ftpconfig.json";
    private static string _ytApiKey = null!;

    public static async Task Main(string[] args)
    {
        _ytApiKey = Environment.GetEnvironmentVariable("YOUTUBE_API_KEY") ?? "";
        if (string.IsNullOrWhiteSpace(_ytApiKey))
        {
            AnsiConsole.MarkupLine("[bold red]YOUTUBE_API_KEY environment variable is not set. Exiting.[/]");
            return;
        }

        if (!File.Exists(ConfigFile)) await AskForCredentials();

        var ftpConfig = LoadConfig();
        if (ftpConfig == null)
            return;

        ShowConfig(ftpConfig);

        while (true)
        {
            AnsiConsole.WriteLine();

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]무엇을 하시겠습니까?[/]")
                    .AddChoices(
                        "채널 선택",
                        "FTP 설정 변경",
                        "종료"
                    )
            );

            switch (action)
            {
                case "채널 선택":
                    await SelectChannel();
                    break;

                case "FTP 설정 변경":
                    await AskForCredentials(ftpConfig);
                    ftpConfig = LoadConfig();
                    if (ftpConfig != null)
                        ShowConfig(ftpConfig);
                    break;

                case "종료":
                    AnsiConsole.MarkupLine("[grey]안녕![/]");
                    return;
            }
        }
    }

    private static FtpConfig? LoadConfig()
    {
        try
        {
            var json = File.ReadAllText(ConfigFile);
            var config = JsonSerializer.Deserialize<FtpConfig>(json);

            if (config == null)
                throw new Exception();

            return config;
        }
        catch
        {
            AnsiConsole.MarkupLine("[bold red]설정 파일을 불러오는 중 오류가 발생했습니다. 다시 설정해 주세요.[/]");
            return null;
        }
    }

    private static void ShowConfig(FtpConfig config)
    {
        AnsiConsole.MarkupLine("\n[bold green]현재 FTP 설정:[/]");
        AnsiConsole.MarkupLine($"호스트: [yellow]{config.Host}[/]");
        AnsiConsole.MarkupLine($"포트: [yellow]{config.Port}[/]");
        AnsiConsole.MarkupLine($"사용자: [yellow]{config.Username}[/]");
        AnsiConsole.MarkupLine("비밀번호: [yellow]********[/]");
        AnsiConsole.MarkupLine($"대상 디렉토리: [yellow]{config.TargetDirectory}[/]\n");
    }

    private static async Task AskForCredentials(FtpConfig? existingConfig = null)
    {
        AnsiConsole.MarkupLine("\n[bold cyan]FTP 연결 설정[/]\n");

        var host = AnsiConsole.Prompt(
            new TextPrompt<string>("FTP 호스트:")
                .DefaultValue(existingConfig?.Host ?? "")
                .Validate(h =>
                    string.IsNullOrWhiteSpace(h)
                        ? ValidationResult.Error("호스트를 입력하십시오.")
                        : ValidationResult.Success()
                )
        );

        var port = AnsiConsole.Prompt(
            new TextPrompt<int>("FTP 포트:")
                .DefaultValue(existingConfig?.Port ?? 21)
                .Validate(p =>
                    p is > 0 and <= 65535
                        ? ValidationResult.Success()
                        : ValidationResult.Error("유효한 포트 번호를 입력하십시오 (1-65535).")
                )
        );

        var username = AnsiConsole.Prompt(
            new TextPrompt<string>("사용자 이름:")
                .DefaultValue(existingConfig?.Username ?? "")
        );

        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("비밀번호:")
                .Secret()
                .AllowEmpty()
        );

        var config = new FtpConfig
        {
            Host = host,
            Port = port,
            Username = username,
            Password = password
        };

        var ftpClient = new AsyncFtpClient(host, username, password, port);

        try
        {
            await AnsiConsole.Progress()
                .AutoClear(true)
                .Columns(
                    new SpinnerColumn()
                )
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("FTP 서버에 연결하는 중...", maxValue: 100);
                    task.IsIndeterminate = true;

                    await ftpClient.AutoConnect();

                    task.StopTask();
                });
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine($"\n[bold red]FTP 서버에 연결하는 중 오류가 발생했습니다: {Markup.Escape(e.Message)}[/]");
            return;
        }

        var currentPath = "/";

        while (true)
        {
            var listing = await ftpClient.GetListing(currentPath);

            var folders = listing
                .Where(i => i.Type == FtpObjectType.Directory)
                .Select(i => Markup.Escape(i.Name))
                .ToList();

            folders.Insert(0, ".."); // go up
            folders.Add("(이 폴더 선택)");

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"위치: [bold]{Markup.Escape(currentPath)}[/]\n대상 디렉토리를 선택하십시오:")
                    .AddChoices(folders)
            );

            if (choice == "(이 폴더 선택)")
                break;

            if (choice == "..")
                currentPath = Path.GetDirectoryName(currentPath.Replace('\\', '/')) ?? "/";
            else
                currentPath = $"{currentPath.TrimEnd('/')}/{choice}";
        }

        AnsiConsole.MarkupLine($"\n선택된 대상 디렉토리: [yellow]{currentPath}[/]");

        config.TargetDirectory = currentPath;

        await File.WriteAllTextAsync(
            ConfigFile,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
        );

        AnsiConsole.MarkupLine("\n[bold green]설정이 저장되었습니다.[/]");
    }

    private static async Task SelectChannel()
    {
        AnsiConsole.MarkupLine("\n----- FtpStellaKirinuki v1.0.0 -----\n");
        AnsiConsole.MarkupLine("시청하고 싶은 채널을 선택하십시오.\n");

        var types = Enum.GetValues<ChannelType>();

        var names = types
            .Select(t => ChannelTypeExtensions.GetColoredName(t))
            .Append("(취소)")
            .ToList();

        var selectedName = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .AddChoices(names)
        );

        if (selectedName == "(취소)")
            return;

        var selectedType = types
            .First(t => ChannelTypeExtensions.Names[t] == selectedName);

        AnsiConsole.MarkupLine($"\n[bold green]{selectedName} 채널이 선택되었습니다![/]\n");

        var playlistId = ChannelTypeExtensions.ChannelPlaylistIds[selectedType];
        var youtube = CreateYoutubeService(_ytApiKey);

        // var videos = await FetchPlaylistVideosAsync(youtube, playlistId);
        var selectedVideos = await SelectVideosCustomAsync(youtube, playlistId);

        AnsiConsole.Clear();

        if (selectedVideos == null || selectedVideos.Count == 0)
            return;

        AnsiConsole.MarkupLine($"\n[bold green]{selectedVideos.Count}개의 영상이 선택되었습니다![/]\n");

        Directory.CreateDirectory("downloads");

        var ytdl = new YoutubeDL
        {
            YoutubeDLPath = "yt-dlp.exe",
            FFmpegPath = "ffmpeg.exe",
            OutputFolder = "downloads",
            OutputFileTemplate = "%(id)s.%(ext)s",
            OverwriteFiles = true
        };

        var options = new OptionSet
        {
            NoContinue = true,
            Format = "bestvideo[height<=720]+bestaudio/best[height<=720]",
            NoPlaylist = true,
            RestrictFilenames = true,
            // Output = "%(id)s.%(ext)s"
        };

        var results = new Dictionary<PlaylistItem, RunResult<string>>();

        await AnsiConsole.Progress()
            .AutoClear(true)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            )
            .StartAsync(async ctx =>
            {
                foreach (var video in selectedVideos)
                {
                    var url = $"https://www.youtube.com/watch?v={video.Snippet.ResourceId.VideoId}";
                    var title = Markup.Escape(video.Snippet.Title);

                    var task = ctx.AddTask($"[cyan]{title}[/]", maxValue: 100);

                    var progress = new Progress<DownloadProgress>(p =>
                    {
                        task.Value = p.Progress * 100;

                        task.Description =
                            $"[cyan]{title}[/] [grey]{p.DownloadSpeed} • ETA {p.ETA}[/]";
                    });

                    var result = await ytdl.RunVideoDownload(
                        url,
                        progress: progress,
                        overrideOptions: options
                    );

                    task.Value = 100;
                    task.StopTask();

                    results.Add(video, result);
                }
            });

        AnsiConsole.MarkupLine("\n[bold green]모든 다운로드가 완료되었습니다![/]\n");

        foreach (var (video, result) in results)
        {
            var title = Markup.Escape(video.Snippet.Title);

            AnsiConsole.MarkupLine(result.Success
                ? $"[green]성공:[/] {title} - [grey]{Markup.Escape(result.Data)}[/]"
                : $"[red]실패:[/] {title} - [grey]{Markup.Escape(string.Join(' ', result.ErrorOutput))}[/]");
        }

        var config = LoadConfig();
        if (config == null)
            return;

        var ftpClient = new AsyncFtpClient(
            config.Host,
            config.Username,
            config.Password,
            config.Port,
            new FluentFTP.FtpConfig()
            {
                UploadDataType = FtpDataType.Binary,
                UploadRateLimit = 0,
                DataConnectionType = FtpDataConnectionType.AutoPassive,
                LocalFileBufferSize = 1024 * 1024
            }
        );

        try
        {
            await AnsiConsole.Progress()
                .AutoClear(true)
                .Columns(
                    new SpinnerColumn()
                )
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("FTP 서버에 연결하는 중...", maxValue: 100);
                    task.IsIndeterminate = true;

                    await ftpClient.AutoConnect();

                    task.StopTask();
                });
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine($"\n[bold red]FTP 서버에 연결하는 중 오류가 발생했습니다: {Markup.Escape(e.Message)}[/]");

            Directory.Delete("downloads", true);

            return;
        }

        AnsiConsole.MarkupLine("\n[bold cyan]FTP 서버로 업로드 중...[/]\n");

        // var semaphore = new SemaphoreSlim(4);

        await AnsiConsole.Progress()
            .AutoClear(true)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            )
            .StartAsync(async ctx =>
            {
                foreach (var (video, result) in results)
                {
                    if (!result.Success || string.IsNullOrWhiteSpace(result.Data))
                    {
                        AnsiConsole.MarkupLine("[grey]스킵: 다운로드 실패 또는 경로 없음[/]");
                        continue;
                    }

                    var localPath = result.Data;
                    Console.WriteLine(localPath);

                    if (!File.Exists(localPath))
                    {
                        AnsiConsole.MarkupLine(
                            $"[red]로컬 파일 없음:[/] {Markup.Escape(localPath)}"
                        );
                        continue;
                    }

                    var fileName = video.Snippet.Title
                        .Replace('/', '_')
                        .Replace('\\', '_')
                        + Path.GetExtension(localPath);
                    var baseDir = config.TargetDirectory.TrimEnd('/');
                    var channelDir = selectedType.ToString().Replace(" ", "_");
                    var remotePath = $"{(baseDir == "" ? "/" : baseDir)}/{channelDir}/{fileName}";
                    var title = Markup.Escape(video.Snippet.Title);

                    AnsiConsole.MarkupLine(
                        $"[grey]업로드 시작:[/] {Markup.Escape(localPath)} → {Markup.Escape(remotePath)}"
                    );

                    var task = ctx.AddTask($"[cyan]{title}[/]", maxValue: 100);

                    try
                    {
                        var status = await ftpClient.UploadFile(
                            localPath,
                            remotePath,
                            createRemoteDir: true,
                            progress: new Progress<FtpProgress>(p =>
                            {
                                task.Value = p.Progress;

                                task.Description =
                                    $"[cyan]{title}[/] [grey]{FormatBytesPerSecond(p.TransferSpeed)}[/]";
                            })
                        );

                        task.Value = 100;
                        task.StopTask();

                        AnsiConsole.MarkupLine(
                            $"[green]업로드 완료:[/] {Markup.Escape(remotePath)} ({status})"
                        );
                    }
                    catch (Exception e)
                    {
                        task.StopTask();

                        AnsiConsole.MarkupLine(
                            $"[red]업로드 실패:[/] {title}\n{Markup.Escape(e.ToString())}"
                        );
                    }
                }

                // var uploadTasks = results.Select(async kvp =>
                // {
                //     var (video, result) = kvp;
                //
                //     if (!result.Success || string.IsNullOrWhiteSpace(result.Data))
                //     {
                //         AnsiConsole.MarkupLine("[grey]스킵: 다운로드 실패 또는 경로 없음[/]");
                //         return;
                //     }
                //
                //     var localPath = result.Data;
                //
                //     if (!File.Exists(localPath))
                //     {
                //         AnsiConsole.MarkupLine(
                //             $"[red]로컬 파일 없음:[/] {Markup.Escape(localPath)}"
                //         );
                //         return;
                //     }
                //
                //     string SanitizeUnixFilename(string name)
                //     {
                //         var invalidChars = Path.GetInvalidFileNameChars();
                //         var sanitized = new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
                //         return sanitized;
                //     }
                //
                //     var fileName = SanitizeUnixFilename(video.Snippet.Title + Path.GetExtension(localPath));
                //     var baseDir = config.TargetDirectory.TrimEnd('/');
                //     var channelDir = selectedType.ToString().Replace(" ", "_");
                //     var remotePath = $"{(baseDir == "" ? "/" : baseDir)}/{channelDir}/{fileName}";
                //     var title = Markup.Escape(video.Snippet.Title);
                //
                //     AnsiConsole.MarkupLine(
                //         $"[grey]업로드 시작:[/] {Markup.Escape(localPath)} → {Markup.Escape(remotePath)}"
                //     );
                //
                //     var task = ctx.AddTask($"[cyan]{title}[/]", maxValue: 100);
                //
                //     await semaphore.WaitAsync();
                //     try
                //     {
                //         var status = await ftpClient.UploadFile(
                //             localPath,
                //             remotePath,
                //             createRemoteDir: true,
                //             progress: new Progress<FtpProgress>(p =>
                //             {
                //                 task.Value = p.Progress;
                //
                //                 task.Description =
                //                     $"[cyan]{title}[/] [grey]{FormatBytesPerSecond(p.TransferSpeed)}[/]";
                //             })
                //         );
                //
                //         task.Value = 100;
                //         task.StopTask();
                //
                //         AnsiConsole.MarkupLine(
                //             $"[green]업로드 완료:[/] {Markup.Escape(remotePath)} ({status})"
                //         );
                //     }
                //     catch (Exception e)
                //     {
                //         task.StopTask();
                //
                //         AnsiConsole.MarkupLine(
                //             $"[red]업로드 실패:[/] {title}\n{Markup.Escape(e.ToString())}"
                //         );
                //     }
                //     finally
                //     {
                //         semaphore.Release();
                //     }
                // });
                //
                // await Task.WhenAll(uploadTasks);
            });


        try
        {
            Directory.Delete("downloads", true);
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine(
                $"[red]downloads 삭제 실패:[/] {Markup.Escape(e.Message)}"
            );
        }

        AnsiConsole.MarkupLine("[grey]FTP 연결 종료 중...[/]");

        await ftpClient.Disconnect();

        AnsiConsole.MarkupLine("\n[bold green]FTP 업로드가 완료되었습니다![/]\n");

        return;

        string FormatBytesPerSecond(double bytesPerSecond)
        {
            string[] sizes = ["B/s", "KB/s", "MB/s", "GB/s", "TB/s"];
            var order = 0;
            while (bytesPerSecond >= 1024 && order < sizes.Length - 1)
            {
                order++;
                bytesPerSecond /= 1024;
            }

            return $"{bytesPerSecond:0.##} {sizes[order]}";
        }
    }

    private static YouTubeService CreateYoutubeService(string apiKey)
    {
        return new YouTubeService(new BaseClientService.Initializer
        {
            ApiKey = apiKey,
            ApplicationName = "FtpStellaKirinuki"
        });
    }

    private static async Task<IList<PlaylistItem>?> SelectVideosCustomAsync(
        YouTubeService youtube,
        string playlistId,
        int pageSize = 20)
    {
        var selected = new HashSet<int>(); // virtual indices
        var cursor = 0;
        var page = 0;

        var pages = new Dictionary<int, IList<PlaylistItem>>();
        var pageTokens = new Dictionary<int, string?> { [0] = null };
        var hasMorePages = true;

        async Task<IList<PlaylistItem>> GetPageAsync(int p)
        {
            if (pages.TryGetValue(p, out var cached))
                return cached;

            if (!pageTokens.ContainsKey(p))
                return new List<PlaylistItem>();

            var request = youtube.PlaylistItems.List("snippet");
            request.PlaylistId = playlistId;
            request.MaxResults = pageSize;
            request.PageToken = pageTokens[p];

            var response = await request.ExecuteAsync();

            pages[p] = response.Items;
            pageTokens[p + 1] = response.NextPageToken;
            if (response.NextPageToken == null)
                hasMorePages = false;

            return response.Items;
        }

        ConsoleKey key;

        do
        {
            var pageVideos = await GetPageAsync(page);

            AnsiConsole.Clear();
            AnsiConsole.MarkupLine(
                $"[bold cyan]영상 선택[/]  [grey](페이지 {page + 1})[/]\n");

            for (var i = 0; i < pageVideos.Count; i++)
            {
                var virtualIndex = page * pageSize + i;
                var isSelected = selected.Contains(virtualIndex);
                var isCursor = i == cursor;

                var prefix = isCursor ? "[yellow]>[/]" : " ";
                var check = isSelected ? "[green](X)[/]" : "[grey]( )[/]";
                var title = Markup.Escape(pageVideos[i].Snippet.Title);

                AnsiConsole.MarkupLine($"{prefix} {check} {title}");
            }

            AnsiConsole.MarkupLine(
                "\n[grey]↑↓ 이동 | <space> 선택 | ←→ 페이지 | Enter 완료 | Esc 취소[/]");

            key = Console.ReadKey(true).Key;

            switch (key)
            {
                case ConsoleKey.UpArrow:
                    cursor = Math.Max(0, cursor - 1);
                    break;

                case ConsoleKey.DownArrow:
                    cursor = Math.Min(pageVideos.Count - 1, cursor + 1);
                    break;

                case ConsoleKey.Spacebar:
                    if (pageVideos.Count > 0)
                    {
                        var idx = page * pageSize + cursor;
                        if (!selected.Add(idx))
                            selected.Remove(idx);
                    }

                    break;

                case ConsoleKey.LeftArrow:
                    if (page > 0)
                    {
                        page--;
                        cursor = 0;
                    }

                    break;

                case ConsoleKey.RightArrow:
                    if (hasMorePages)
                    {
                        var next = await GetPageAsync(page + 1);
                        if (next.Count > 0)
                        {
                            page++;
                            cursor = 0;
                        }
                    }

                    break;

                case ConsoleKey.Escape:
                    AnsiConsole.Clear();
                    return null;
            }
        } while (key != ConsoleKey.Enter);

        AnsiConsole.Clear();

        // Resolve virtual indices → PlaylistItem
        return selected
            .OrderBy(i => i)
            .Select(i =>
            {
                var p = i / pageSize;
                var offset = i % pageSize;
                return pages[p][offset];
            })
            .ToList();
    }
}