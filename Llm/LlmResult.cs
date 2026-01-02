namespace VoiceR.Llm
{
    public class LlmResult {
        public string Prompt { get; set; }  = string.Empty;
        public string Response { get; set; } = string.Empty;
        public int InputTokens { get; set; } = 0;
        public int OutputTokens { get; set; } = 0;
        public decimal EstimatedInputPriceUSD { get; set; } = 0;
        public decimal EstimatedOutputPriceUSD { get; set; } = 0;
        public long ElapsedMilliseconds { get; set; } = 0;
    }
}
