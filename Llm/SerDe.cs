using System;
using System.Collections.Generic;
using VoiceR.Model;

namespace VoiceR.Llm
{
    /// <summary>
    /// Serializes a tree of UI elements to a string representation. Typically used to send as context to the LLM.
    /// </summary>
    public interface ISerializer
    {
        string Serialize(Item item);
        string Format { get; }
    }

    /// <summary>
    /// Deserializes an LLM repsonse to a list of actions.
    /// </summary>
    public interface IDeserializer
    {
        void ExtractActionsFromResponse(string response, out List<Action> actions, out List<string> errors);
        string Format { get; }
    }
}