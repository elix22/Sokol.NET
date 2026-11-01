using Sokol.GUI;
using Xunit;

namespace Scene.Tests
{
    /// <summary>NumberInput.SignificantDigits: a non-zero value the decimals would show as zero shows in short scientific form.</summary>
    public sealed class NumberInputTests
    {
        [Theory]
        [InlineData(2.375e-6f, 0, "0.0000")]      // off (default): unchanged
        [InlineData(2.375e-6f, 3, "2.38e-6")]     // the float is 2.37499989e-6: its printed digits are rounded, not the binary value
        [InlineData(9.996e-6f, 3, "1e-5")]        // rounding carries into the exponent
        [InlineData(1.5e-40f, 3, "1.5e-40")]      // subnormal
        [InlineData(0.04f, 3, "4e-2", 1)]         // 1 decimal: shown as 0.0 otherwise
        [InlineData(-4.25e-9f, 3, "-4.25e-9")]
        [InlineData(4e-5f, 3, "4e-5")]            // trailing mantissa zeros dropped
        [InlineData(1e-4f, 3, "0.0001")]          // shown by the decimals: unchanged
        [InlineData(0.0085f, 3, "0.0085")]
        [InlineData(0.70713f, 3, "0.7071")]
        [InlineData(0f, 3, "0.0000")]
        [InlineData(123.5f, 3, "123.5000")]
        public void Shows(float value, int digits, string text, int decimals = 4)
        {
            var ni = new NumberInput { DecimalPlaces = decimals, SignificantDigits = digits, Value = value };
            Assert.Equal(text, ni.Text);
            Assert.Equal(value, ni.Value);   // the exact value, not the display
        }

        [Theory]
        [InlineData(-0f, 1, "0.0")]
        [InlineData(-0.04f, 1, "0.0")]
        [InlineData(-0.4f, 0, "0")]
        [InlineData(-0.05f, 1, "-0.1")]
        [InlineData(-12.5f, 2, "-12.50")]
        public void ShownZero_HasNoSign(float value, int decimals, string text)
        {
            var ni = new NumberInput { DecimalPlaces = decimals, Value = value };
            Assert.Equal(text, ni.Text);
            Assert.Equal(value, ni.Value);
        }

        [Fact]
        public void Integers_NeverScientific()
        {
            var ni = new NumberInput { IsInteger = true, SignificantDigits = 3, Value = 0.25f };
            Assert.Equal("0", ni.Text);
        }

        [Theory]
        [InlineData(3, "2.5e-6", "2.5e-6", 2.5e-6f)]
        [InlineData(0, "2.5e-6", "2.56", 2.56f)]   // off: 'e' and the inner '-' are refused, as before
        public void Typing_TakesAnExponent(int digits, string typed, string shown, float value)
        {
            var ni = new NumberInput { DecimalPlaces = 4, SignificantDigits = digits, Value = 1f };
            ni.Text = "";
            foreach (char c in typed) ni.OnTextInput(new KeyEvent { CharCode = c });
            Assert.Equal(shown, ni.Text);
            Assert.Equal(value, ni.Value);   // what Enter / focus loss commits
        }
    }
}
