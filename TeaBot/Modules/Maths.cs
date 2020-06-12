using System;
using System.Threading.Tasks;
using Discord.Commands;
using TeaBot.Commands;

namespace TeaBot.Modules
{
    [Summary("Various math commands")]
    public class Maths : TeaInteractiveBase
    {
        [Command("solvequadratic")]
        [Summary("Solves a quadratic equation of the look `ax^2 + bx + c = 0` with given coefficients")]
        public async Task SolveQuadratic(double a, double b, double c)
        {

            if (a == 0 || b == 0 || c == 0)
            {
                await ReplyAsync(":x: Neither of the coefficients can be 0!");
                return;
            }

            double d = Math.Pow(b, 2) - 4 * a * c;

            await ReplyAsync(
                (a < 0 ? "-" : "") + (Math.Abs(a) == 1 ? "" : Math.Abs(a).ToString()) + "x^2" +
                (b < 0 ? " - " : " + ") + (Math.Abs(b) == 1 ? "" : Math.Abs(b).ToString()) + "x" +
                (c < 0 ? " - " : " + ") + Math.Abs(c).ToString() + " = 0" + "\n" + SolveEquation()
            );

            string SolveEquation()
            {
                if (d > 0)
                {
                    double x1 = (-b + Math.Sqrt(d)) / (2 * a);
                    double x2 = (-b - Math.Sqrt(d)) / (2 * a);
                    return ("x1 = " + x1 + "\n" + "x2 = " + x2);
                }
                else if (d == 0)
                {
                    double x = -b / (2 * a);
                    return ("x1 = x2 = " + x);
                }
                else
                {
                    return "No real roots";
                }
            }

        }

        [Command("random")]
        [Alias("rand")]
        [Summary("Produces a random number within the range `[min; max]`")]
        public async Task Random(int min, int max)
        {
            if (min > max)
            {
                await ReplyAsync("The minimum value cannot be larger than the maximum value!");
                return;
            }

            await ReplyAsync(new Random().Next(min, max + 1).ToString());
        }

        [Command("prime")]
        [Summary("Determines whether a given number (in range of 64 bits) is prime")]
        public async Task Prime(long number)
        {
            number = Math.Abs(number);

            if (number == 1 || number == 0)
            {
                await ReplyAsync("Not prime");
                return;
            }

            long squareRoot = (long)Math.Sqrt(number);
            for (int i = 2; i <= squareRoot; i++)
            {
                if (number % i == 0)
                {
                    await ReplyAsync("Not prime");
                    return;
                }
            }

            await ReplyAsync("Prime");
        }

        [Command("equation", RunMode = RunMode.Async)]
        [Summary("Trains your basic math skills")]
        public async Task Equation()
        {
            int x1 = new Random().Next(20, 150);
            int x2 = new Random().Next(20, 150);

            await ReplyAsync($"{x1} + {x2} = ?");

            var response = await NextMessageAsync(true, true, TimeSpan.FromSeconds(10));

            if (response == null)
            {
                await ReplyAsync("You didn't answer in time!");
                return;
            }

            if (!int.TryParse(response.Content, out int guess))
            {
                await ReplyAsync("That is not a number!");
                return;
            }

            if (guess == 69 && x1 + x2 == guess)
            {
                await ReplyAsync("NICE!");
                return;
            }

            if (guess == x1 + x2) await ReplyAsync("Correct!");
            else await ReplyAsync("Wrong!");
        }
    }
}
