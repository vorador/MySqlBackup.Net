namespace System.Security.Cryptography
{
    public class CryptoExpress
    {
        public static string ConvertByteArrayToHexString(byte[] ba)
        {
            if (ba == null || ba.Length == 0)
                return "";

            char[] c = new char[ba.Length * 2 + 2];
            c[0] = '0'; c[1] = 'x';
            for (int y = 0, x = 2; y < ba.Length; ++y, ++x)
            {
                var b = ((byte)(ba[y] >> 4));
                c[x] = (char)(b > 9 ? b + 0x37 : b + 0x30);
                b = ((byte)(ba[y] & 0xF));
                c[++x] = (char)(b > 9 ? b + 0x37 : b + 0x30);
            }
            return new string(c);
        }
    }
}
