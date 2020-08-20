using ConMon.Services;
using Shouldly;
using System;
using System.IO;
using Xunit;

namespace ConMon.Tests
{
    public class TokenResolutionTests
    {
        [Theory]
        [InlineData("<::last::/>", "./c")]
        [InlineData("<b/::last::/>", "b/09")]
        [InlineData("< b/::last::/ >", "b/09")]
        [InlineData("<b/::last::/foo.txt>", "b/09/foo.txt")]
        [InlineData("<  b/::last::/foo.txt  >", "b/09/foo.txt")]
        [InlineData("<b/09/::last::/>", "b/09/09")]
        [InlineData("<b/09/::last::/foo.txt>", "b/09/09/foo.txt")]
        [InlineData("<b/::last::/::last::/>", "b/09/09")]
        [InlineData("<b/::last::/::last::/foo.txt>", "b/09/09/foo.txt")]
        [InlineData("border:{<::last::/>}", "border:{./c}")]
        public void InputShouldResolve(string input, string expectation)
        {
            const string baseDirectory = nameof(TokenResolutionTests) + "." + nameof(InputShouldResolve);
            var startDirectory = Environment.CurrentDirectory;

            try
            {
                if (Directory.Exists(baseDirectory)) Directory.Delete(baseDirectory, true);
                Directory.CreateDirectory(baseDirectory);
                Environment.CurrentDirectory = baseDirectory;

                Directory.CreateDirectory("a");
                Directory.CreateDirectory("b");
                Directory.CreateDirectory("c");
                for (var i = 0; i < 10; i++)
                {
                    Directory.CreateDirectory($"b/{i:00}");
                    File.WriteAllText($"b/{i:00}/foo.txt", string.Empty);
                }
                for (var i = 0; i < 10; i++)
                {
                    Directory.CreateDirectory($"b/09/{i:00}");
                    File.WriteAllText($"b/09/{i:00}/foo.txt", string.Empty);
                }

                var result = ApplicationService.ResolveTokenLast(input);
                if (!input.StartsWith("border:"))
                {
                    if (result.EndsWith(".txt"))
                        File.Exists(result).ShouldBeTrue($"File '{result}' does not exist.");
                    else
                        Directory.Exists(result).ShouldBeTrue($"Directory '{result}' does not exist.");
                }
                
                result
                    .Replace("\\", "/") // Ensure compatibility between Windows and Unix results.
                    .ShouldBe(expectation);
            }
            finally
            {
                Environment.CurrentDirectory = startDirectory;
            }
        }

        [Theory]
        [InlineData("<::last::>")]
        [InlineData("<foo/::last::>")]
        [InlineData("border:{<::last::>}")]
        public void EndingToTokenShouldThrow(string input)
        {
            Should.Throw<InvalidOperationException>(() => ApplicationService.ResolveTokenLast(input));
        }
    }
}