using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

public class RedisBase : IDisposable
{
    public readonly string __MONITOR_CHANNEL = "<{__MONITOR__}>";

    internal static readonly byte[] _END_DATA = new byte[] { 13, 10 }; //= \r\n
    internal static byte[] __combine(int size, params byte[][] arrays)
    {
        byte[] rv = new byte[size];
        int offset = 0;
        foreach (byte[] array in arrays)
        {
            System.Buffer.BlockCopy(array, 0, rv, offset, array.Length);
            offset += array.Length;
        }
        return rv;
    }

    private Socket socket = null;
    private BufferedStream bstream = null;
    private NetworkStream networkStream = null;
    internal NetworkStream m_stream { get { return networkStream; } }
    internal RedisSetting m_setting { get; }

    internal RedisBase(RedisSetting setting)
    {
        if (setting != null)
        {
            m_setting = setting;

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;
            socket.ReceiveTimeout = m_setting.ReceiveTimeout;
            socket.ReceiveBufferSize = m_setting.ReceiveBufferSize;

            Connect();
        }
    }

    void Connect()
    {
        socket.Connect(m_setting.Host,m_setting.Port);
        if (!this._connected)
        {
            socket.Close();
            socket = null;
            return;
        }
        networkStream = new NetworkStream(socket);
        bstream = new BufferedStream(networkStream, m_setting.BufferedStreamSize);
    }

    internal bool _connected
    {
        get
        {
            return socket != null && socket.Connected;
        }
    }

    public bool SelectDb(int indexDb)
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("*2\r\n");
            sb.Append("$6\r\nSELECT\r\n");
            sb.AppendFormat("${0}\r\n{1}\r\n", indexDb.ToString().Length, indexDb);
            byte[] buf = Encoding.UTF8.GetBytes(sb.ToString());
            bool ok = SendBuffer(buf);
            string line = ReadLine();
            return ok && !string.IsNullOrEmpty(line) && line[0] == '+';
        }
        catch (Exception ex)
        {
        }
        return false;
    }

    internal bool PUBLISH(string channel, string value)
    {
        if (!this._connected) return false;
        if (string.IsNullOrEmpty(channel)) return false;

        if (channel != __MONITOR_CHANNEL) channel = "<{" + channel + "}>";
        channel = channel.ToUpper();

        try
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("*3\r\n");
            sb.Append("$7\r\nPUBLISH\r\n");
            sb.AppendFormat("${0}\r\n{1}\r\n", channel.Length, channel);
            //sb.AppendFormat("${0}\r\n{1}\r\n", value.Length, value);

            byte[] vals = Encoding.UTF8.GetBytes(value);
            sb.AppendFormat("${0}\r\n", vals.Length);
            byte[] buf = Encoding.UTF8.GetBytes(sb.ToString());

            var arr = __combine(buf.Length + vals.Length + 2, buf, vals, _END_DATA);

            var ok = SendBuffer(arr);
            var line = ReadLine();
            //Console.WriteLine("->" + line);
            return ok;
        }
        catch (Exception ex)
        {
        }
        return false;
    }

    internal bool SendBuffer(byte[] buf)
    {
        if (socket == null) Connect();
        if (socket == null) return false;

        try { socket.Send(buf); }
        catch (SocketException ex)
        {
            // timeout;
            socket.Close();
            socket = null;
            return false;
        }
        return true;
    }

    internal string ReadLine()
    {
        StringBuilder sb = new StringBuilder();
        int c;
        while ((c = bstream.ReadByte()) != -1)
        {
            if (c == '\r')
                continue;
            if (c == '\n')
                break;
            sb.Append((char)c);
        }
        return sb.ToString();
    }

    internal string ReadString()
    {
        var result = ReadBuffer();
        if (result != null)
            return Encoding.UTF8.GetString(result);
        return null;
    }

    internal string[] ReadMultiString()
    {
        string r = ReadLine();
        //Log(string.Format("R: {0}", r));
        if (r.Length == 0)
            throw new Exception("Zero length respose");

        char c = r[0];
        if (c == '-')
            throw new Exception(r.StartsWith("-ERR") ? r.Substring(5) : r.Substring(1));

        List<string> result = new List<string>();

        if (c == '*')
        {
            int n;
            if (Int32.TryParse(r.Substring(1), out n))
                for (int i = 0; i < n; i++)
                {
                    string str = ReadString();
                    result.Add(str);
                }
        }
        return result.ToArray();
    }

    internal byte[] ReadBuffer()
    {
        string s = ReadLine();
        //Log("S", s);
        if (s.Length == 0)
            throw new ResponseException("Zero length respose");

        char c = s[0];
        if (c == '-')
            throw new ResponseException(s.StartsWith("-ERR ") ? s.Substring(5) : s.Substring(1));

        if (c == '$')
        {
            if (s == "$-1")
                return null;
            int n;

            if (Int32.TryParse(s.Substring(1), out n))
            {
                byte[] retbuf = new byte[n];

                int bytesRead = 0;
                do
                {
                    int read = bstream.Read(retbuf, bytesRead, n - bytesRead);
                    if (read < 1)
                        throw new ResponseException("Invalid termination mid stream");
                    bytesRead += read;
                }
                while (bytesRead < n);
                if (bstream.ReadByte() != '\r' || bstream.ReadByte() != '\n')
                    throw new ResponseException("Invalid termination");
                return retbuf;
            }
            throw new ResponseException("Invalid length");
        }

        /* don't treat arrays here because only one element works -- use DataArray!
		//returns the number of matches
		if (c == '*') {
			int n;
			if (Int32.TryParse(s.Substring(1), out n)) 
				return n <= 0 ? new byte [0] : ReadData();			
			throw new ResponseException ("Unexpected length parameter" + r);
		}
		*/

        if (c == ':')
            return Encoding.ASCII.GetBytes(s);

        throw new ResponseException("Unexpected reply: " + s);
    }




    public bool HSET(long key, int field, byte[] value)
        => HMSET(key.ToString(), new Dictionary<string, byte[]>() { { field.ToString(), value } });

    public bool HMSET(string key, IDictionary<string, string> fields)
    {
        if (!this._connected) return false;

        var dic = new Dictionary<string, byte[]>();
        foreach (var kv in fields)
            dic.Add(kv.Key, Encoding.UTF8.GetBytes(kv.Value));
        return HMSET(key, dic);
    }

    public bool HMSET(string key, IDictionary<string, byte[]> fields)
    {
        if (!this._connected) return false;

        if (fields == null || fields.Count == 0) return false;
        try
        {
            StringBuilder bi = new StringBuilder();
            bi.AppendFormat("*{0}\r\n", 2 + fields.Count * 2);
            bi.Append("$5\r\nHMSET\r\n");
            bi.AppendFormat("${0}\r\n{1}\r\n", key.Length, key);

            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buf = Encoding.UTF8.GetBytes(bi.ToString());
                ms.Write(buf, 0, buf.Length);

                string keys_ = key;
                if (fields != null && fields.Count > 0)
                {
                    foreach (var data in fields)
                    {
                        buf = Encoding.UTF8.GetBytes(string.Format("${0}\r\n{1}\r\n", data.Key.Length, data.Key));
                        ms.Write(buf, 0, buf.Length);
                        buf = Encoding.UTF8.GetBytes(string.Format("${0}\r\n", data.Value.Length));
                        ms.Write(buf, 0, buf.Length);
                        ms.Write(data.Value, 0, data.Value.Length);
                        ms.Write(_END_DATA, 0, 2);
                        keys_ += "|" + data.Key;
                    }
                }
                var ok = SendBuffer(ms.ToArray());
                string line = ReadLine();
                if (ok && !string.IsNullOrEmpty(line) && (line[0] == '+' || line[0] == ':'))
                    return true;
                return false;
            }
        }
        catch (Exception ex)
        {
        }
        return false;
    }


    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (socket != null)
        {
            socket.Close();
            socket = null;
        }
    }
}
