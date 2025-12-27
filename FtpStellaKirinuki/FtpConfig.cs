namespace FtpStellaKirinuki;

public class FtpConfig
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 21;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string TargetDirectory { get; set; } = "/";
}