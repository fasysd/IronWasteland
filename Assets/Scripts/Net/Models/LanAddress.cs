namespace IronWasteland.Net.Models
{
    /// <summary>Chuẩn hoá ô nhập địa chỉ host: IP thuần, IP:cổng hoặc tên máy.</summary>
    public static class LanAddress
    {
        public static bool TryParse(string input, ushort defaultPort, out string host, out ushort port)
        {
            host = string.Empty;
            port = defaultPort;
            if (string.IsNullOrWhiteSpace(input) || defaultPort == 0)
            {
                return false;
            }

            string text = input.Trim();
            int colon = text.IndexOf(':');
            if (colon >= 0)
            {
                if (colon == 0 || colon != text.LastIndexOf(':') || colon == text.Length - 1)
                {
                    return false;
                }

                string portText = text.Substring(colon + 1).Trim();
                if (!ushort.TryParse(portText, out port) || port == 0)
                {
                    return false;
                }

                host = text.Substring(0, colon).Trim();
            }
            else
            {
                host = text;
            }

            return host.Length > 0;
        }
    }
}
