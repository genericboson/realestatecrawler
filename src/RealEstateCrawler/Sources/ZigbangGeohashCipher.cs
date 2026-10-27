using System.Security.Cryptography;
using System.Text;

namespace RealEstateCrawler.Sources;

/// <summary>
/// 직방 웹은 매물 조회 API 의 geohash 파라미터를 암호화해서 보낸다.
/// AES-256-CBC(PKCS7), IV = HMAC-SHA256(key, geohash) 의 앞 16바이트, 결과 = Base64(IV ‖ 암호문).
/// </summary>
public sealed class ZigbangGeohashCipher(string base64Key)
{
    readonly byte[] _key = Convert.FromBase64String(base64Key);

    public string Encrypt(string geohash)
    {
        var plain = Encoding.UTF8.GetBytes(geohash);
        var iv = HMACSHA256.HashData(_key, plain)[..16];
        using var aes = Aes.Create();
        aes.Key = _key;
        var cipher = aes.EncryptCbc(plain, iv, PaddingMode.PKCS7);
        return Convert.ToBase64String([.. iv, .. cipher]);
    }
}
