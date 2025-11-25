using System;
using System.IO;
using System.Security.Cryptography;

using UnityEngine;

using Sirenix.OdinInspector;

public class CryptoManager : MonoBehaviour
{
    private string outputFilePath;
    
    // The key and IV you should use for AES encryption
    private string aes_key = "AXe8YwuIn1zxt3FPWTZFlAa14EHdPAdN9FaZ9RQWihc="; //44자
    private string aes_iv = "bsxnWolsAyO7kCfWuyrnqg=="; //24자
    
    [FilePath(Extensions = "$DynamicBGMExtensions", AbsolutePath = true)] 
    public string bgmPath;

    [FolderPath(AbsolutePath = true)] 
    public string outputPath;

    [BoxGroup("Member referencing")] 
    public string DynamicBGMExtensions = "mp3, ogg";

    [Button("Encrypt", ButtonSizes.Medium)]
    public void Encrypt()
    {
        // Read the MP3 file into memory
        byte[] plaintext = File.ReadAllBytes(bgmPath);

        // Encrypt the MP3 file using AES
        byte[] ciphertext = _Encrypt(plaintext);

        // Write the encrypted MP3 file to disk
        this.outputFilePath = this.outputPath + "/encrypted.mp3";
        File.WriteAllBytes(this.outputFilePath, ciphertext);
    }

    [Button("Decrypt", ButtonSizes.Medium)]
    public void DeCrypt()
    {
        byte[] ciphertext = File.ReadAllBytes(this.outputFilePath);
        
        // Decrypt the MP3 file using AES
        byte[] decrypted = Decrypt(ciphertext);

        // Write the decrypted MP3 file to disk
        string _outputPath = this.outputPath + "/decrypted.mp3";
        File.WriteAllBytes(_outputPath, decrypted);
    }
    
    private byte[] _Encrypt(byte[] plaintext)
    {
        // Create an AES encryption algorithm
        Aes encryptionAlgorithm = Aes.Create();
        encryptionAlgorithm.KeySize = 256;
        encryptionAlgorithm.Key = Convert.FromBase64String(this.aes_key);
        encryptionAlgorithm.IV = Convert.FromBase64String(this.aes_iv);

        // Create a memory stream to store the encrypted MP3 file
        using (MemoryStream memoryStream = new MemoryStream())
        {
            // Create a crypto stream to encrypt the data
            using (CryptoStream cryptoStream = new CryptoStream(memoryStream,
                       encryptionAlgorithm.CreateEncryptor(), CryptoStreamMode.Write))
            {
                // Encrypt the MP3 data
                cryptoStream.Write(plaintext, 0, plaintext.Length);
                cryptoStream.FlushFinalBlock();

                // Return the encrypted data as a byte array
                return memoryStream.ToArray();
            }
        }
    }
    
    private byte[] Decrypt(byte[] ciphertext)
    {
        // Create an AES encryption algorithm
        Aes encryptionAlgorithm = Aes.Create();
        encryptionAlgorithm.KeySize = 256;
        encryptionAlgorithm.Key = Convert.FromBase64String(this.aes_key);
        encryptionAlgorithm.IV = Convert.FromBase64String(this.aes_iv);

        // Create a memory stream to store the decrypted MP3 file
        using (MemoryStream memoryStream = new MemoryStream())
        {
            // Create a crypto stream to decrypt the data
            using (CryptoStream cryptoStream = new CryptoStream(memoryStream,
                       encryptionAlgorithm.CreateDecryptor(), CryptoStreamMode.Write))
            {
                // Decrypt the MP3 data
                cryptoStream.Write(ciphertext, 0, ciphertext.Length);
                cryptoStream.FlushFinalBlock();

                // Return the decrypted data as a byte array
                return memoryStream.ToArray();
            }
        }
    }
}
