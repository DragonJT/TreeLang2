
static class Program
{
    static void Main()
    {
        List<Token> tokens = Tokenizer.Tokenize(File.ReadAllText("langSrc/test.txt"));
        Parser.Create();
        var result = Parser.Parse("Root", new TokenReader(tokens));
        if (result.valid)
        {
            Console.WriteLine(result.tree);
            Console.WriteLine("=======================");
            VM.Init(result.tree);
            VM.Call("Main", []);
        }
        else
        {
            Console.WriteLine("error");
        }
    }
}