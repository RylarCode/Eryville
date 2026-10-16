using System;

// Everything the game needs from "the server". The game only talks to this interface, so the same game runs
// against a stand-in that lives inside the game (LocalBackend: files on the device, rolls done here) or, later,
// a real online one (Firebase) that keeps the data and does the rolls on the server, where players cannot tamper
// with them.
//
// Every call reports back through a callback. A local backend calls it straight away, before the call returns;
// an online one calls it later when the answer arrives. The game must therefore never assume the answer is
// already there when a call returns.
public interface IGameBackend
{
    string Name { get; }

    // The server's idea of the current time (UTC). Local: the device's clock. Online: the device clock corrected
    // by the difference to the server, so changing the phone's clock does not help a cheater.
    DateTime ServerUtcNow { get; }

    // Asks the server for its time again (online); nothing to do locally
    void SyncTime(Action<BackendResult> done);

    // The whole saved game as one piece of text, under a name (a "slot")
    void SaveBlob(string slot, string json, Action<BackendResult> done);
    void LoadBlob(string slot, Action<BackendResult, string> done); // json is null when there is no save
    void HasBlob(string slot, Action<bool> done);
    void DeleteBlob(string slot, Action<BackendResult> done);

    // Things the server decides. The answer says what the player's balances are afterwards; the game shows them.
    void Pull(PullRequest request, Action<PullResponse> done);
    void BuyLetter(Action<BuyLetterResponse> done);
}
