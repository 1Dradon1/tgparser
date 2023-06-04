using System.Text.Json;
using TdLib.Bindings;
using YamlDotNet.Serialization;
using static TdLib.TdApi;

namespace TdLib.Samples.GetChats;

public static class Program
{
    
    private static int ApiId;
    private static string ApiHash = null!;
    private static string PhoneNumber = null!;
    private static string ApplicationVersion = null!;
    private static string ChatsReportFileName = null!;
    private static string ProfileReportFileName = null!;
    private static string ChatUsersReportFileName = null!;
    private static string UserIdsReportFileName = null!;
    private static string UserIdsFileName = null!;
    private static int ChannelLimit;
    private static int IterationsCount;
    private static int Limit;

    public static Serializer Serializer = new Serializer();

    private static TdClient _client = null!;
    private static readonly ManualResetEventSlim ReadyToAuthenticate = new();

    private static bool _authNeeded;
    private static bool _passwordNeeded;

    private static async Task Main()
    {
        var configText = System.IO.File.ReadAllText("rawData\\cfg.json");
        var config = JsonSerializer.Deserialize<AppConfig>(configText)!;
        ApiId = config.ApiId;
        ApiHash = config.ApiHash;
        PhoneNumber = config.PhoneNumber;
        ApplicationVersion = config.ApplicationVersion;
        ChatsReportFileName = config.ChatsReportFileName;
        ProfileReportFileName = config.ProfileReportFileName;
        ChatUsersReportFileName = config.ChatUsersReportFileName;
        UserIdsReportFileName = config.UserIdsReportFileName;
        UserIdsFileName = config.UserIdsFileName;
        ChannelLimit = config.ChannelLimit;
        IterationsCount = config.IterationsCount;
        Limit = config.Limit;

        if (Limit > 89)
        {
            Console.WriteLine("Limit <= 89");
            Console.ReadLine();
            return;
        }

        _client = new TdClient();
        _client.Bindings.SetLogVerbosityLevel(TdLogLevel.Fatal);
        _client.UpdateReceived += async (_, update) => { await ProcessUpdates(update); };
        ReadyToAuthenticate.Wait();

        if (_authNeeded)
        {
            // Interactively handling authentication
            await HandleAuthentication();
        }

        Console.Clear();

        var currentUser = await GetCurrentUser();

        var fullUserName = $"{currentUser.FirstName} {currentUser.LastName}".Trim();
        Console.WriteLine($"Successfully logged in as [{currentUser.Id}] / [@{currentUser.Usernames?.ActiveUsernames[0]}] / [{fullUserName}]");
        await UserMenu();
        
        Console.ReadLine();
    }

    private static async Task LoadChatData()
    {
        var chats = await _client.GetChatsAsync(limit: 10);

        foreach (var chatId in chats.ChatIds)
        {
            var chat = await _client.ExecuteAsync(new GetChat { ChatId = chatId});
            var t = _client.GetType();
            Console.WriteLine("1");
        }
    }

    private static async Task GetSelfDataParsingAction()
    {
        var currentUser = await GetCurrentUser();
        await FileSizeByActionHandler(ProfileReportFileName, action2: (writer) => SaveProfileReport(writer, currentUser));
    }

    private static async Task GetChatInfoParsingAction()
        => await FileSizeByActionHandler(ChatsReportFileName, action1: SaveChannelsReport);

    private static async Task GetChatUsersParsingAction()
    {
        Console.WriteLine("Input group id");
        var groupId = long.Parse(Console.ReadLine()!);
        var chatMembers = await GetChatUsers(groupId);
        await FileSizeByActionHandler(ChatUsersReportFileName, action2: (writer) => SaveChatFullInfo(writer, chatMembers));
    }

    private static async Task GetUserReportsParsingAction()
        => await FileSizeByActionHandler(UserIdsReportFileName, action1: (writer) => SaveUserReports(writer, GetMemberIdsFromFile()));
    private static void PrintPossibleActions()
    {
        Console.WriteLine("Choose action");
        Console.WriteLine("1. Parse self data");
        Console.WriteLine("2. Parse chat info");
        Console.WriteLine("3. Parse chat full info");
        Console.WriteLine("4. Parse users reports");
    }

    private static async Task UserMenu()
    {
        var actionByNumber = new Dictionary<string, Func<Task>>
        {
            ["1"] = GetSelfDataParsingAction,
            ["2"] = GetChatInfoParsingAction,
            ["3"] = GetChatUsersParsingAction,
            ["4"] = GetUserReportsParsingAction
        };

        PrintPossibleActions();
        
        while (true)
        {
            var line = Console.ReadLine()!;

            if (!actionByNumber.TryGetValue(line, out var action))
            {
                Console.WriteLine("incorrect choiсe!");
                continue;
            }

            await action!();
        }
    }

    private static async Task FileSizeByActionHandler(string fileName, Func<StreamWriter, Task>? action1 = null, Action<StreamWriter>? action2 = null)
    {
        if (action1 == null && action2 == null)
            throw new ArgumentException();

        await PrintFileSizeDifference(fileName, async () =>
        {
            using var writer = new StreamWriter(fileName);
            if (action1 != null)
                await action1(writer);
            else
                action2!(writer);
        });
    }

    private static long[] GetMemberIdsFromFile()
        => System.IO.File.ReadAllLines(UserIdsFileName).Select(long.Parse).ToArray();

    private static async Task SaveUserReports(StreamWriter writer, long[] userIds)
    {
        //await LoadChatData();
        //Thread.Sleep(5000);
        foreach (var userId in userIds)
        {
            //var user = await _client.GetUserAsync(userId);
            //var userReport = await _client.GetUserFullInfoAsync(userId);
            //writer.SerializeAndWrite(user);
            //writer.SerializeAndWrite(userReport);?????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
            Thread.Sleep(1500);
        }
    }

    private static void SaveChatFullInfo(StreamWriter writer, BasicGroupFullInfo chatMembers)
        => writer.SerializeAndWrite(chatMembers);

    private static async Task<BasicGroupFullInfo> GetChatUsers(long chatId)
    {
        await _client.ExecuteAsync(new GetBasicGroup { BasicGroupId = chatId });
        return await _client.ExecuteAsync(new GetBasicGroupFullInfo { BasicGroupId = chatId });
    }

    private static long GetStartFileSize(string fileName)
        => System.IO.File.Exists(fileName) ? new FileInfo(fileName).Length : 0;

     private async static Task PrintFileSizeDifference(string fileName, Func<Task> fileWriter)
    {
        var startFileSize = GetStartFileSize(fileName);
        await fileWriter();
        var endFileSize = new FileInfo(fileName).Length;
        Console.WriteLine("----------------");
        Console.WriteLine($"File \"{fileName}\" size was changed on: {endFileSize - startFileSize} bytes");
        Console.WriteLine("----------------");

    }

    private static async Task SaveChannelsReport(StreamWriter writeToFile)
    {
        Console.WriteLine("Chat in progress!");
        var channels = GetChannels(ChannelLimit);
        await foreach (var channel in channels)
            await SaveChannelAndShowProgress(writeToFile, channel);
    }

    private static async Task HandleAuthentication()
    {
        // Setting phone number
        await _client.ExecuteAsync(new SetAuthenticationPhoneNumber
        {
            PhoneNumber = PhoneNumber
        });

        // Telegram servers will send code to us
        Console.Clear();
        Console.Write("Insert the login code: ");
        var code = Console.ReadLine();

        await _client.ExecuteAsync(new CheckAuthenticationCode
        {
            Code = code
        });

        if (!_passwordNeeded) { return; }

        // 2FA may be enabled. Cloud password is required in that case.
        Console.Write("Insert the password: ");
        var password = Console.ReadLine();

        await _client.ExecuteAsync(new CheckAuthenticationPassword
        {
            Password = password
        });
    }

    private static async Task ProcessUpdates(Update update)
    {

        switch (update)
        {
            case Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitTdlibParameters }:
                var filesLocation = Path.Combine(AppContext.BaseDirectory, "db");
                await _client.ExecuteAsync(new SetTdlibParameters
                {
                    ApiId = ApiId,
                    ApiHash = ApiHash,
                    DeviceModel = "PC",
                    SystemLanguageCode = "en",
                    ApplicationVersion = ApplicationVersion,
                    DatabaseDirectory = filesLocation,
                    FilesDirectory = filesLocation,
                    // More parameters available!
                });
                break;

            case Update.UpdateAuthorizationState { AuthorizationState: AuthorizationState.AuthorizationStateWaitPhoneNumber }:
            case Update.UpdateAuthorizationState { AuthorizationState: AuthorizationState.AuthorizationStateWaitCode }:
                _authNeeded = true;
                ReadyToAuthenticate.Set();
                break;

            case Update.UpdateAuthorizationState { AuthorizationState: AuthorizationState.AuthorizationStateWaitPassword }:
                _authNeeded = true;
                _passwordNeeded = true;
                ReadyToAuthenticate.Set();
                break;

            case Update.UpdateUser:
                ReadyToAuthenticate.Set();
                break;

            case Update.UpdateConnectionState { State: ConnectionState.ConnectionStateReady }:
                // You may trigger additional event on connection state change
                break;

            default:
                // ReSharper disable once EmptyStatement
                ;
                // Add a breakpoint here to see other events
                break;
        }
    }

    private static async Task<User> GetCurrentUser()
        => await _client.ExecuteAsync(new GetMe());

    private static async IAsyncEnumerable<Chat> GetChannels(int limit)
    {
        var chats = await _client.ExecuteAsync(new TdApi.GetChats
        {
            Limit = limit
        });
        foreach (var chatId in chats.ChatIds)
        {
            var chat = await _client.ExecuteAsync(new GetChat
            {
                ChatId = chatId
            });

            if (chat.Type is ChatType.ChatTypeSupergroup or ChatType.ChatTypeBasicGroup or ChatType.ChatTypePrivate)
            {
                yield return chat;
            }
        }
    }

    private static async Task<Messages> GetMessages(long chatId, long fromMessageId, int offset, int limit)
       => await _client.ExecuteAsync(new GetChatHistory { ChatId = chatId, FromMessageId = fromMessageId, Offset = offset, Limit = limit, OnlyLocal = false});

    private static void SerializeAndSaveFirstMessage(StreamWriter writer, Message message)
        => writer.SerializeAndWrite(message);
    
    private static void SaveProfileReport(StreamWriter writer, User currentUser)
        => writer.SerializeAndWrite(currentUser);

    private static async Task SaveChannelAndShowProgress(StreamWriter writeToFile, Chat channel)
    {
        var isLastMessage = false;
        var firstMessage = await GetMessages(channel.Id, 0, 0, 1);
        var lastMessageId = channel.LastMessage.Id;

        SerializeAndSaveFirstMessage(writeToFile, firstMessage.Messages_.First());

        IntroduceChannelProgress(channel);
        for (var i = 0; i < IterationsCount && !isLastMessage; i++)
        {
            var messages = await GetMessages(channel.Id, lastMessageId, 0, Limit);
            SerializeAndSaveData(messages, writeToFile, lastMessageId, ref lastMessageId, ref isLastMessage);

            ShowProgress(i);
            Thread.Sleep(1500);
        }

        EndProgress();
    }

    #region progress bar

    private static void IntroduceChannelProgress(Chat channel)
    {
        Console.WriteLine($"Channel in progress: {channel.Title}");
    }

    private static void EndProgress()
    {
        Console.Write('\r');
        Console.WriteLine("100%");
    }

    private static double CalculateProgress(int i)
      => Math.Round(((double)i / IterationsCount) * 100);

    private static void ShowProgress(int i)
    {
        var progress = CalculateProgress(i);
        Console.Write('\r');
        Console.Write($"{progress}%");
    }

    #endregion

    private static void SerializeAndSaveData(
        Messages messages,
        StreamWriter writer,
        long lastMessageId,
        ref long messageId,
        ref bool isLastMessage)
    {
        foreach (var message in messages.Messages_)
        {
            if (messageId == lastMessageId)
                isLastMessage = true;
            var serializedYamlData = Serializer.Serialize((dynamic)message);
            messageId = message.Id;
            writer.WriteLine(serializedYamlData);
            writer.Flush();
        }
        isLastMessage = false;
    }
}
