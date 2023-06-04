namespace TdLib.Samples.GetChats;

internal class AppConfig
{
    public int ApiId { get; set; }
    public string ApiHash { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string ApplicationVersion { get; set; } = null!;
    public string ChatsReportFileName { get; set; } = null!;
    public string ProfileReportFileName { get; set; } = null!;
    public string ChatUsersReportFileName { get; set; } = null!;
    public string UserIdsReportFileName { get; set; } = null!;
    public string UserIdsFileName { get; set; } = null!;
    public int ChannelLimit { get; set; }
    public int IterationsCount { get; set; }
    public int Limit { get; set;}

}