// ============================================================================
// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Proprietary rights reserved except as expressly licensed herein.
//
// DO NOT PANIC PORTFOLIO VISUALIZER
// This file is governed by the SANYALnet Labs Non-Commercial License in the
// root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
// for AI/ML model training are prohibited unless separately authorized.
//
// Attribution is required: "Based on original work by Supratim Sanyal of
// SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
// patent, trademark, and governing-law provisions.
// ============================================================================
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Text;
using DoNotPanicPortfolioVisualizer.Core.Storage;
using DoNotPanicPortfolioVisualizer.Data.Interfaces;

namespace DoNotPanicPortfolioVisualizer.Data.Services;

public sealed class SettingsProtectionService : ISettingsProtectionService
{
    private const string EnvelopePrefix = "dnppv2-aesgcm-v1";
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private readonly string _keyPath;
    private readonly object _sync = new();

    public SettingsProtectionService(string? keyPath = null)
    {
        _keyPath = StorageOverridePathValidator.ResolveFilePath(
            keyPath,
            Path.Combine(LocalDataRootResolver.ResolveForCurrentPlatform().SecretRoot, "settings-protection.key"));
    }

    public string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return string.Empty;

        byte[] key = LoadOrCreateKey();
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] ciphertext = new byte[plaintextBytes.Length];
        byte[] tag = new byte[TagSizeBytes];
        using (AesGcm aes = new(key, TagSizeBytes))
        {
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }

        return string.Join(
            ':',
            EnvelopePrefix,
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag),
            Convert.ToBase64String(ciphertext));
    }

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
            return string.Empty;

        string[] parts = protectedText.Split(':', StringSplitOptions.None);
        if (parts.Length != 4 || !string.Equals(parts[0], EnvelopePrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Protected settings secret is not in the expected DNPPV-2.0 format.");

        byte[] key = LoadOrCreateKey();
        byte[] nonce = Convert.FromBase64String(parts[1]);
        byte[] tag = Convert.FromBase64String(parts[2]);
        byte[] ciphertext = Convert.FromBase64String(parts[3]);
        byte[] plaintext = new byte[ciphertext.Length];
        using (AesGcm aes = new(key, TagSizeBytes))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return Encoding.UTF8.GetString(plaintext);
    }

    private byte[] LoadOrCreateKey()
    {
        lock (_sync)
        {
            string? directory = Path.GetDirectoryName(_keyPath);
            if (!string.IsNullOrWhiteSpace(directory))
                EnsureCurrentUserOnlyDirectoryPermissions(directory);

            if (File.Exists(_keyPath))
            {
                EnsureCurrentUserOnlyPermissions(_keyPath);
                return ReadExistingKey(_keyPath);
            }

            byte[] generated = RandomNumberGenerator.GetBytes(KeySizeBytes);
            WriteKeyFile(generated);
            return generated;
        }
    }

    private void WriteKeyFile(byte[] key)
    {
        string directory = Path.GetDirectoryName(_keyPath) ?? Environment.CurrentDirectory;
        string tempPath = Path.Combine(directory, Path.GetFileName(_keyPath) + "." + Path.GetRandomFileName() + ".tmp");

        try
        {
            using (FileStream stream = new(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new(stream, Encoding.UTF8))
            {
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

                writer.Write(Convert.ToBase64String(key));
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            EnsureCurrentUserOnlyPermissions(tempPath);
            File.Move(tempPath, _keyPath);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
            }
        }
    }

    private static byte[] ReadExistingKey(string path)
    {
        try
        {
            string encoded = File.ReadAllText(path).Trim();
            byte[] key = Convert.FromBase64String(encoded);
            if (key.Length != KeySizeBytes)
            {
                throw new InvalidOperationException("Settings protection key is invalid and cannot be used.");
            }

            return key;
        }
        catch (FormatException ex)
        {
            _ = path;
            throw new InvalidOperationException(
                "Settings protection key is corrupted and cannot be decoded.", ex);
        }
    }

    private static void EnsureCurrentUserOnlyPermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            ApplyWindowsAcl(new FileInfo(path));
            return;
        }

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static void EnsureCurrentUserOnlyDirectoryPermissions(string path)
    {
        Directory.CreateDirectory(path);

        if (OperatingSystem.IsWindows())
        {
            ApplyWindowsAcl(new DirectoryInfo(path));
            return;
        }

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyWindowsAcl(FileSystemInfo fileSystemInfo)
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier? currentUser = identity.User;
        if (currentUser is null)
            throw new InvalidOperationException("Unable to resolve the current Windows user SID for secret protection.");

        (InheritanceFlags inheritanceFlags, PropagationFlags propagationFlags) = fileSystemInfo switch
        {
            DirectoryInfo => (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None),
            _ => (InheritanceFlags.None, PropagationFlags.None)
        };

        FileSystemSecurity security = fileSystemInfo switch
        {
            DirectoryInfo => new DirectorySecurity(),
            _ => new FileSecurity()
        };

        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(currentUser);
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser,
            FileSystemRights.FullControl,
            inheritanceFlags,
            propagationFlags,
            AccessControlType.Allow));

        switch (fileSystemInfo)
        {
            case DirectoryInfo directoryInfo:
                directoryInfo.SetAccessControl((DirectorySecurity)security);
                break;
            case FileInfo fileInfo:
                fileInfo.SetAccessControl((FileSecurity)security);
                break;
        }
    }
}
