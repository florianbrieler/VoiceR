using System;
using System.Collections.Generic;
using VoiceR.Model;

namespace VoiceR.Llm
{
    public interface ISerDe
    {
        string Serialize(Item item);
        void ExtractActionsFromResponse(string response, out List<Action> actions, out List<string> errors);
        string Format { get; }
    }
}