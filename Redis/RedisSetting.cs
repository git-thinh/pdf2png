public class RedisSetting
{
    public REDIS_TYPE Type { get; }

    public string Host { get; }
    public int Port { get; }
    public int Db { get; }
    public string Password { get; }

    public int BufferedStreamSize { get; set; }
    public int ReceiveBufferSize { get; set; }

    public int ReceiveTimeout { get; set; }
    public int SendTimeout { get; set; }

    public bool PublishToMonitor { get; set; }

    public bool NotifyByItemKey { get; set; }
    public bool NotifyByHashKey { get; set; }

    public RedisSetting(REDIS_TYPE type, string host = "localhost", int port = 6379, int db = 0, string password = "")
    {
        this.Type = type;
        this.Host = host;
        this.Port = port;
        this.Db = db;
        this.Password = password;

        settingByType();
    }

    public RedisSetting(REDIS_TYPE type, int port = 6379)
    {
        this.Type = type;
        this.Host = "localhost";
        this.Port = port;
        this.Db = 0;
        this.Password = string.Empty;

        settingByType();
    }

    void settingByType()
    {
        switch (this.Type)
        {
            case REDIS_TYPE.ONLY_READ:
                this.SendTimeout = 5000; // 5 seconds
                this.ReceiveTimeout = 3 * 60 * 1000; // 3 minus

                this.ReceiveBufferSize = 512 * 1024; // 8kb || 64kb
                this.BufferedStreamSize = 512 * 1024; // Buffering for once readByte()

                this.PublishToMonitor = false;
                this.NotifyByHashKey = false;
                this.NotifyByItemKey = false;
                break;
            case REDIS_TYPE.ONLY_SUBCRIBE:
                this.SendTimeout = 1000; // 1 seconds
                this.ReceiveTimeout = 15000; // 15 second

                this.ReceiveBufferSize = 255 * 1024; // 8kb || 64kb
                this.BufferedStreamSize = 8 * 1024; // Buffering for once readByte()

                this.PublishToMonitor = false;
                this.NotifyByHashKey = false;
                this.NotifyByItemKey = false;
                break;
            case REDIS_TYPE.ONLY_WRITE:
                this.SendTimeout = 3 * 60 * 1000; // 3 minus
                this.ReceiveTimeout = 5000; // 5 seconds

                this.ReceiveBufferSize = 8 * 1024; // 8kb || 64kb
                this.BufferedStreamSize = 8 * 1024; // Buffering for once readByte()

                this.PublishToMonitor = true;
                this.NotifyByHashKey = false;
                this.NotifyByItemKey = false;
                break;
        }
    }
}
