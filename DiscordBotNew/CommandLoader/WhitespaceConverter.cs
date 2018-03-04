using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBotNew.CommandLoader
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    namespace BinaryAsWhitespace
    {
        /// <summary>
        /// Encodes arbitrary bytestrings using zero-width whitespace characters.
        /// Written by Glen Husman (@glen3b on GitHub)
        /// </summary>
        public static class WhitespaceConverter
        {
            // value -> char
            // length must be a power of two
            // furthermore, lg(length) must evenly divide a byte (1,2,4,8)
            // we do this to avoid splitting across byte lines when encoding/decoding
            // first element = 0, second = 1, etc
            private static readonly char[] CharsByValue = new char[4] {
            '\u200B', // ZERO WIDTH SPACE
            '\uFEFF', // ZERO WIDTH NO-BREAK SPACE
            '\u180E', // MONGOLIAN VOWEL SEPARATOR
            '\u00AD' // SOFT HYPHEN
        };

            private const int BitsPerByte = 8;
            public static IReadOnlyList<char> CharactersByValue { get; }
            public static IReadOnlyDictionary<char, byte> ValuesByCharacter { get; }
            public static int BitsPerCharacter => bitsPerCharacter;
            public static int CharactersPerByte => charactersPerByte;
            public static byte BitMask => bitMask;
            private static readonly int bitsPerCharacter;
            private static readonly int charactersPerByte;
            /// <summary>
            /// Mask for the bits that can be encoded by one character.
            /// </summary>
            private static readonly byte bitMask;

            private static int LogBase2(int value)
            {
                // ASSUMPTION: "value" is a power of two
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Cannot take the logarithm of nonpositive numbers.");
                }

                int log = 0;

                while (value > 1)
                {
                    log++;
                    value >>= 1;
                }

                return log;
            }

            static WhitespaceConverter()
            {
                // check if values set size is a power of two
                if (CharsByValue.Length == 0)
                {
                    throw new InvalidOperationException("No character conversions are defined.");
                }

                if ((CharsByValue.Length & (CharsByValue.Length - 1)) != 0)
                {
                    throw new InvalidOperationException("CharsByValue's length is not a power of two.");
                }

                bitsPerCharacter = LogBase2(CharsByValue.Length);
                switch (bitsPerCharacter)
                {
                    case 1:
                    case 2:
                    case 4:
                    case 8:
                        // not a problem
                        break;
                    default:
                        throw new InvalidOperationException("Invalid number of bits per character. Bytes must evenly divide into encoded characters.");
                }
                bitMask = (byte)(CharsByValue.Length - 1);
                charactersPerByte = BitsPerByte / bitsPerCharacter;

                CharactersByValue = CharsByValue;
                ValuesByCharacter = CharsByValue.Select((c, i) => new { Char = c, Value = (byte)i }).ToDictionary(v => v.Char, v => v.Value);
            }

            public static void ConvertToWhitespace(byte[] input, int inputIndex, int inputLength, char[] output, int outputIndex)
            {
                int outputLength = inputLength * charactersPerByte;

                if (inputIndex + inputLength > input.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(input), "Index out of bounds.");
                }

                if (outputIndex + outputLength > output.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(output), "Index out of bounds.");
                }

                // running output index
                int j = outputIndex;

                for (int i = 0; i < inputLength; i++)
                {
                    byte valueToRead = input[i + inputIndex];
                    for (int k = 0; k < charactersPerByte; k++)
                    {
                        // this is the value that will be translated to a char
                        byte significantValue = (byte)((valueToRead & (bitMask << (bitsPerCharacter * k))) >> bitsPerCharacter * k);
                        output[j++] = CharsByValue[significantValue];
                    }
                }
            }

            public static void ConvertFromWhitespace(char[] input, int inputIndex, int inputLength, byte[] output, int outputIndex)
            {
                if (inputLength % charactersPerByte != 0)
                {
                    throw new ArgumentException("The given input length cannot possibly correspond to a binary string.", nameof(inputLength));
                }

                int outputLength = inputLength / charactersPerByte;

                if (inputIndex + inputLength > input.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(input), "Index out of bounds.");
                }

                if (outputIndex + outputLength > output.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(output), "Index out of bounds.");
                }

                // running input index
                int j = inputIndex;

                for (int i = 0; i < outputLength; i++)
                {
                    byte valueToWrite = 0;
                    for (int k = 0; k < charactersPerByte; k++)
                    {
                        if (!ValuesByCharacter.TryGetValue(input[j], out byte encodedValue))
                        {
                            throw new ArgumentException($"Input contains an invalid character at index {j}.", nameof(input));
                        }
                        j++;

                        valueToWrite |= (byte)(encodedValue << (bitsPerCharacter * k));
                    }
                    output[i + outputIndex] = valueToWrite;
                }
            }

            public static char[] ConvertToWhitespace(byte[] input) => ConvertToWhitespace(input, 0, input.Length);

            public static char[] ConvertToWhitespace(byte[] input, int inputIndex, int inputLength)
            {
                char[] newChars = new char[inputLength * charactersPerByte];
                ConvertToWhitespace(input, inputIndex, inputLength, newChars, 0);
                return newChars;
            }

            public static byte[] ConvertFromWhitespace(char[] input) => ConvertFromWhitespace(input, 0, input.Length);

            public static byte[] ConvertFromWhitespace(char[] input, int inputIndex, int inputLength)
            {
                byte[] newBytes = new byte[inputLength / charactersPerByte];
                ConvertFromWhitespace(input, inputIndex, inputLength, newBytes, 0);
                return newBytes;
            }
        }
    }
}
