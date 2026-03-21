namespace Infosphere.Postgresql.Db;

internal sealed record CommandOptions(string Command)
{
    public static CommandOptions Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new("migrate");
        }

        return new(args[0].Trim().ToLowerInvariant());
    }
}
