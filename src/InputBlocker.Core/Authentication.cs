using System.Security.Cryptography;
using System.Text;

namespace InputBlocker.Core;
public static class Authentication
{
    public const int WorkFactor = 600_000;
    public static PasswordRecord Create(string password)
    {
        if (password.EnumerateRunes().Count() < 8 || password.Length > 1024) throw new ArgumentException("Use 8 or more characters (maximum 1024 UTF-16 units).");
        byte[] salt = RandomNumberGenerator.GetBytes(32);
        return new(Convert.ToBase64String(salt), Convert.ToBase64String(Derive(password, salt, WorkFactor)));
    }
    public static void Validate(PasswordRecord record)
    {
        if (record.Iterations is < WorkFactor or > 2_000_000 || Convert.FromBase64String(record.Salt).Length != 32 || Convert.FromBase64String(record.Hash).Length != 32)
            throw new InvalidDataException("Invalid password record.");
    }
    public static bool Verify(string password, PasswordRecord record)
    {
        Validate(record);
        if (password.Length > 1024) return false;
        var derived = Derive(password, Convert.FromBase64String(record.Salt), record.Iterations);
        try { return CryptographicOperations.FixedTimeEquals(derived, Convert.FromBase64String(record.Hash)); }
        finally { CryptographicOperations.ZeroMemory(derived); }
    }
    public static PasswordRecord Change(string current, string next, PasswordRecord record, bool requireCurrent = true)
    {
        if (requireCurrent && !Verify(current, record)) throw new UnauthorizedAccessException("Current password is incorrect.");
        return Create(next);
    }
    private static byte[] Derive(string password, byte[] salt, int iterations)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(password);
        try { return Rfc2898DeriveBytes.Pbkdf2(bytes, salt, iterations, HashAlgorithmName.SHA256, 32); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
public sealed class AttemptThrottle
{
    public int Failures { get; private set; }
    public DateTimeOffset NextAttempt { get; private set; }
    public bool CanAttempt(DateTimeOffset now) => now >= NextAttempt;
    public void Failed(DateTimeOffset now) { Failures++; NextAttempt = now.AddSeconds(Failures < 3 ? 0 : Math.Min(30, Failures * 2)); }
    public void Reset() { Failures = 0; NextAttempt = default; }
}
