using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// AES-256-GCM encryption for save files.
/// Unity's Mono runtime doesn't support System.Security.Cryptography.AesGcm, so GCM (counter mode + GHASH,
/// NIST SP 800-38D) is implemented here on top of the plain AES block cipher.
///
/// Password files:  "SRRSAVE" | version (1) | salt (16) | iterations (4, big-endian) | nonce (12) | ciphertext | tag (16)
/// The key is PBKDF2-HMAC-SHA256(password, salt). The password itself is never stored.
/// Everything before the ciphertext is authenticated too, so any change to the file makes it fail to open.
/// </summary>
public static class SaveCrypto
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("SRRSAVE");
    private const byte Version = 1;
    public const int KeySize = 32, SaltSize = 16, NonceSize = 12, TagSize = 16;
    public const int DefaultIterations = 100000;
    private const int MaxIterations = 5000000;
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    public class WrongPasswordException : Exception
    {
        public WrongPasswordException() : base("Wrong password, or the file was changed.") { }
    }

    // ===== PASSWORD FILES =====

    public static byte[] EncryptWithPassword(byte[] plaintext, string password, int iterations = DefaultIterations)
    {
        byte[] salt = RandomBytes(SaltSize);
        byte[] nonce = RandomBytes(NonceSize);
        byte[] key = DeriveKey(password, salt, iterations);

        int headerSize = Magic.Length + 1 + SaltSize + 4 + NonceSize;
        byte[] file = new byte[headerSize + plaintext.Length + TagSize];
        int o = 0;
        Buffer.BlockCopy(Magic, 0, file, o, Magic.Length); o += Magic.Length;
        file[o++] = Version;
        Buffer.BlockCopy(salt, 0, file, o, SaltSize); o += SaltSize;
        WriteUInt32BE(file, o, (uint)iterations); o += 4;
        Buffer.BlockCopy(nonce, 0, file, o, NonceSize); o += NonceSize;

        byte[] header = new byte[headerSize];
        Buffer.BlockCopy(file, 0, header, 0, headerSize);

        Encrypt(key, nonce, plaintext, header, out byte[] ciphertext, out byte[] tag);
        Buffer.BlockCopy(ciphertext, 0, file, headerSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, file, headerSize + ciphertext.Length, TagSize);
        Array.Clear(key, 0, key.Length);
        return file;
    }

    /// <summary>Throws WrongPasswordException for a wrong password or a modified file, FormatException for other files.</summary>
    public static byte[] DecryptWithPassword(byte[] file, string password)
    {
        int headerSize = Magic.Length + 1 + SaltSize + 4 + NonceSize;
        if (file == null || file.Length < headerSize + TagSize) throw new FormatException("Not a save file.");
        for (int i = 0; i < Magic.Length; i++)
            if (file[i] != Magic[i]) throw new FormatException("Not a save file.");
        if (file[Magic.Length] != Version) throw new FormatException("This save file is from a newer version of the game.");

        int o = Magic.Length + 1;
        byte[] salt = Slice(file, o, SaltSize); o += SaltSize;
        uint iterations = ReadUInt32BE(file, o); o += 4;
        byte[] nonce = Slice(file, o, NonceSize);
        if (iterations < 1 || iterations > MaxIterations) throw new FormatException("Damaged save file.");

        byte[] header = Slice(file, 0, headerSize);
        byte[] ciphertext = Slice(file, headerSize, file.Length - headerSize - TagSize);
        byte[] tag = Slice(file, file.Length - TagSize, TagSize);

        byte[] key = DeriveKey(password, salt, (int)iterations);
        try { return Decrypt(key, nonce, ciphertext, header, tag); }
        finally { Array.Clear(key, 0, key.Length); }
    }

    // ===== RAW KEY (local progress file) =====

    /// <summary>nonce (12) | ciphertext | tag (16)</summary>
    public static byte[] EncryptWithKey(byte[] plaintext, byte[] key, byte[] aad)
    {
        byte[] nonce = RandomBytes(NonceSize);
        Encrypt(key, nonce, plaintext, aad, out byte[] ciphertext, out byte[] tag);
        byte[] result = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);
        return result;
    }

    public static byte[] DecryptWithKey(byte[] data, byte[] key, byte[] aad)
    {
        if (data == null || data.Length < NonceSize + TagSize) throw new FormatException("Damaged data.");
        byte[] nonce = Slice(data, 0, NonceSize);
        byte[] ciphertext = Slice(data, NonceSize, data.Length - NonceSize - TagSize);
        byte[] tag = Slice(data, data.Length - TagSize, TagSize);
        return Decrypt(key, nonce, ciphertext, aad, tag);
    }

    // ===== KEYS =====

    public static byte[] DeriveKey(string password, byte[] salt, int iterations)
    {
        return Pbkdf2Sha256(Encoding.UTF8.GetBytes(password ?? ""), salt, iterations, KeySize);
    }

    public static byte[] Sha256(string text)
    {
        using (SHA256 sha = SHA256.Create())
            return sha.ComputeHash(Encoding.UTF8.GetBytes(text));
    }

    public static byte[] RandomBytes(int count)
    {
        byte[] bytes = new byte[count];
        Rng.GetBytes(bytes);
        return bytes;
    }

    // PBKDF2 (RFC 8018) with HMAC-SHA256
    public static byte[] Pbkdf2Sha256(byte[] password, byte[] salt, int iterations, int length)
    {
        byte[] output = new byte[length];
        using (var hmac = new HMACSHA256(password))
        {
            int blocks = (length + 31) / 32;
            byte[] input = new byte[salt.Length + 4];
            Buffer.BlockCopy(salt, 0, input, 0, salt.Length);

            for (int block = 1; block <= blocks; block++)
            {
                WriteUInt32BE(input, salt.Length, (uint)block);
                byte[] u = hmac.ComputeHash(input);
                byte[] t = (byte[])u.Clone();
                for (int i = 1; i < iterations; i++)
                {
                    u = hmac.ComputeHash(u);
                    for (int k = 0; k < t.Length; k++) t[k] ^= u[k];
                }
                int offset = (block - 1) * 32;
                Buffer.BlockCopy(t, 0, output, offset, Math.Min(32, length - offset));
            }
        }
        return output;
    }

    // ===== AES-GCM =====

    public static void Encrypt(byte[] key, byte[] nonce, byte[] plaintext, byte[] aad, out byte[] ciphertext, out byte[] tag)
    {
        using (Aes aes = CreateAes(key))
        using (ICryptoTransform block = aes.CreateEncryptor())
        {
            byte[] j0 = MakeJ0(nonce);
            ciphertext = Gctr(block, Inc32(j0), plaintext);
            tag = ComputeTag(block, j0, aad ?? new byte[0], ciphertext);
        }
    }

    public static byte[] Decrypt(byte[] key, byte[] nonce, byte[] ciphertext, byte[] aad, byte[] tag)
    {
        using (Aes aes = CreateAes(key))
        using (ICryptoTransform block = aes.CreateEncryptor())
        {
            byte[] j0 = MakeJ0(nonce);
            byte[] expected = ComputeTag(block, j0, aad ?? new byte[0], ciphertext);
            if (!FixedTimeEquals(expected, tag)) throw new WrongPasswordException();
            return Gctr(block, Inc32(j0), ciphertext);
        }
    }

    private static Aes CreateAes(byte[] key)
    {
        if (key == null || key.Length != KeySize) throw new ArgumentException("AES-256 needs a 32-byte key.");
        Aes aes = Aes.Create();
        aes.Mode = CipherMode.ECB;   // Only used as the raw block cipher for GCM
        aes.Padding = PaddingMode.None;
        aes.Key = key;
        return aes;
    }

    private static byte[] EncryptBlock(ICryptoTransform block, byte[] input)
    {
        byte[] output = new byte[16];
        block.TransformBlock(input, 0, 16, output, 0);
        return output;
    }

    private static byte[] MakeJ0(byte[] nonce)
    {
        if (nonce == null || nonce.Length != NonceSize) throw new ArgumentException("GCM needs a 12-byte nonce.");
        byte[] j0 = new byte[16];
        Buffer.BlockCopy(nonce, 0, j0, 0, NonceSize);
        j0[15] = 1;
        return j0;
    }

    private static byte[] Inc32(byte[] counter)
    {
        byte[] next = (byte[])counter.Clone();
        WriteUInt32BE(next, 12, ReadUInt32BE(next, 12) + 1);
        return next;
    }

    // Counter mode
    private static byte[] Gctr(ICryptoTransform block, byte[] icb, byte[] data)
    {
        byte[] output = new byte[data.Length];
        byte[] counter = icb;
        for (int offset = 0; offset < data.Length; offset += 16)
        {
            byte[] keyStream = EncryptBlock(block, counter);
            int n = Math.Min(16, data.Length - offset);
            for (int i = 0; i < n; i++) output[offset + i] = (byte)(data[offset + i] ^ keyStream[i]);
            counter = Inc32(counter);
        }
        return output;
    }

    private static byte[] ComputeTag(ICryptoTransform block, byte[] j0, byte[] aad, byte[] ciphertext)
    {
        byte[] h = EncryptBlock(block, new byte[16]);
        ulong hh = ReadUInt64BE(h, 0), hl = ReadUInt64BE(h, 8);

        ulong yh = 0, yl = 0;
        GhashUpdate(ref yh, ref yl, hh, hl, aad);
        GhashUpdate(ref yh, ref yl, hh, hl, ciphertext);

        // Lengths in bits
        yh ^= (ulong)aad.Length * 8;
        yl ^= (ulong)ciphertext.Length * 8;
        GfMultiply(ref yh, ref yl, hh, hl);

        byte[] s = new byte[16];
        WriteUInt64BE(s, 0, yh);
        WriteUInt64BE(s, 8, yl);

        byte[] ekj0 = EncryptBlock(block, j0);
        byte[] tag = new byte[TagSize];
        for (int i = 0; i < TagSize; i++) tag[i] = (byte)(ekj0[i] ^ s[i]);
        return tag;
    }

    // GHASH over data, zero-padded to whole blocks
    private static void GhashUpdate(ref ulong yh, ref ulong yl, ulong hh, ulong hl, byte[] data)
    {
        byte[] blockBytes = new byte[16];
        for (int offset = 0; offset < data.Length; offset += 16)
        {
            int n = Math.Min(16, data.Length - offset);
            Array.Clear(blockBytes, 0, 16);
            Buffer.BlockCopy(data, offset, blockBytes, 0, n);
            yh ^= ReadUInt64BE(blockBytes, 0);
            yl ^= ReadUInt64BE(blockBytes, 8);
            GfMultiply(ref yh, ref yl, hh, hl);
        }
    }

    // X = X * H in GF(2^128) (SP 800-38D, Algorithm 1). Bit 0 = most significant bit of the first byte.
    private static void GfMultiply(ref ulong xh, ref ulong xl, ulong hh, ulong hl)
    {
        ulong zh = 0, zl = 0, vh = hh, vl = hl;
        for (int i = 0; i < 128; i++)
        {
            ulong bit = i < 64 ? (xh >> (63 - i)) & 1UL : (xl >> (127 - i)) & 1UL;
            if (bit == 1UL) { zh ^= vh; zl ^= vl; }

            bool lsb = (vl & 1UL) != 0;
            vl = (vl >> 1) | (vh << 63);
            vh >>= 1;
            if (lsb) vh ^= 0xE100000000000000UL;
        }
        xh = zh;
        xl = zl;
    }

    // ===== BYTES =====

    private static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    private static byte[] Slice(byte[] source, int offset, int count)
    {
        byte[] result = new byte[count];
        Buffer.BlockCopy(source, offset, result, 0, count);
        return result;
    }

    private static uint ReadUInt32BE(byte[] b, int o) =>
        ((uint)b[o] << 24) | ((uint)b[o + 1] << 16) | ((uint)b[o + 2] << 8) | b[o + 3];

    private static void WriteUInt32BE(byte[] b, int o, uint v)
    {
        b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
    }

    private static ulong ReadUInt64BE(byte[] b, int o) =>
        ((ulong)ReadUInt32BE(b, o) << 32) | ReadUInt32BE(b, o + 4);

    private static void WriteUInt64BE(byte[] b, int o, ulong v)
    {
        WriteUInt32BE(b, o, (uint)(v >> 32));
        WriteUInt32BE(b, o + 4, (uint)v);
    }
}
