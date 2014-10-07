namespace LicenseVerificationLibrary.Obfuscator
{
    using System;
    using System.Text;

    using Java.IO;
    using Java.Lang;
    using Java.Security;
    using Java.Security.Spec;

    using Javax.Crypto;
    using Javax.Crypto.Spec;

    /// <summary>
    /// An Obfuscator that uses AES to encrypt data.
    /// </summary>
    public class AesObfuscator : IObfuscator
    {
        #region Constants and Fields

        /// <summary>
        /// The cipher algorithm.
        /// </summary>
        private const string CipherAlgorithm = "AES/CBC/PKCS5Padding";

        /// <summary>
        /// The header.
        /// </summary>
        private const string Header = "com.android.vending.licensing.AESObfuscator-1|";

        /// <summary>
        /// The keygen algorithm.
        /// </summary>
        private const string KeygenAlgorithm = "PBEWITHSHAAND256BITAES-CBC-BC";

        /// <summary>
        /// The iv.
        /// </summary>
        private static readonly byte[] Iv = new byte[]
            {
               16, 74, 71, 80, 32, 101, 47, 72, 117, 14, 0, 29, 70, 65, 12, 74 
            };

        /// <summary>
        /// The decryptor.
        /// </summary>
        private readonly Cipher decryptor;

        /// <summary>
        /// The encryptor.
        /// </summary>
        private readonly Cipher encryptor;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AesObfuscator"/> class. 
        /// The aes obfuscator.
        /// </summary>
        /// <param name="salt">
        /// an array of random bytes to use for each (un)obfuscation
        /// </param>
        /// <param name="applicationId">
        /// application identifier, e.g. the package name
        /// </param>
        /// <param name="deviceId">
        /// device identifier. Use as many sources as possible to 
        /// create this unique identifier.
        /// </param>
        public AesObfuscator(byte[] salt, string applicationId, string deviceId)
        {
            try
            {
                SecretKeyFactory factory = SecretKeyFactory.GetInstance(KeygenAlgorithm);
                IKeySpec keySpec = new PBEKeySpec((applicationId + deviceId).ToCharArray(), salt, 1024, 256);
                ISecretKey tmp = factory.GenerateSecret(keySpec);
                ISecretKey secret = new SecretKeySpec(tmp.GetEncoded(), "AES");
                this.encryptor = Cipher.GetInstance(CipherAlgorithm);
                this.encryptor.Init(CipherMode.EncryptMode, secret, new IvParameterSpec(Iv));
                this.decryptor = Cipher.GetInstance(CipherAlgorithm);
                this.decryptor.Init(CipherMode.DecryptMode, secret, new IvParameterSpec(Iv));
            }
            catch (GeneralSecurityException e)
            {
                // This can't happen on a compatible Android device.
                throw new RuntimeException("Invalid environment", e);
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// The obfuscate.
        /// </summary>
        /// <param name="original">
        /// The original.
        /// </param>
        /// <param name="key">
        /// The key.
        /// </param>
        /// <returns>
        /// The obfuscate.
        /// </returns>
        /// <exception cref="RuntimeException">
        /// </exception>
        /// <exception cref="RuntimeException">
        /// </exception>
        public string Obfuscate(string original, string key)
        {
            if (original == null)
            {
                return null;
            }

            try
            {
                // Header is appended as an integrity check
                var output = Encoding.UTF8.GetBytes(Header + key + original);
                var doFinal = this.encryptor.DoFinal(output);
                var base64String = Convert.ToBase64String(doFinal);
                return base64String;
            }
            catch (UnsupportedEncodingException e)
            {
                throw new RuntimeException("Invalid environment", e);
            }
            catch (GeneralSecurityException e)
            {
                throw new RuntimeException("Invalid environment", e);
            }
        }

        /// <summary>
        /// The unobfuscate.
        /// </summary>
        /// <param name="obfuscated">
        /// The obfuscated.
        /// </param>
        /// <param name="key">
        /// The key.
        /// </param>
        /// <returns>
        /// The unobfuscate.
        /// </returns>
        /// <exception cref="ValidationException">
        /// </exception>
        /// <exception cref="RuntimeException">
        /// </exception>
        public string Unobfuscate(string obfuscated, string key)
        {
            if (obfuscated == null)
            {
                return null;
            }

            try
            {
                var fromBase64String = Convert.FromBase64String(obfuscated);
                var doFinal = this.decryptor.DoFinal(fromBase64String);
                var result = Encoding.UTF8.GetString(doFinal);

                // Check for presence of header. This serves as an integrity check, 
                // for cases where the block size is correct during decryption.
                var headerAndKey = Header + key;
                if (!result.StartsWith(headerAndKey))
                {
                    throw new ValidationException("Header not found (invalid data or key)" + ":" + obfuscated);
                }

                return result.Substring(headerAndKey.Length);
            }
            catch (FormatException e)
            {
                throw new ValidationException(e.Message + ":" + obfuscated);
            }
            catch (IllegalArgumentException e)
            {
                throw new ValidationException(e.Message + ":" + obfuscated);
            }
            catch (IllegalBlockSizeException e)
            {
                throw new ValidationException(e.Message + ":" + obfuscated);
            }
            catch (BadPaddingException e)
            {
                throw new ValidationException(e.Message + ":" + obfuscated);
            }
            catch (UnsupportedEncodingException e)
            {
                throw new RuntimeException("Invalid environment", e);
            }
        }

        #endregion
    }
}