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

    // True when the server is the authority for letters, pity and pull counts (the game then shows what the server
    // says and cannot change them). False for the local stand-in, where the game keeps them itself.
    bool ServerOwnsBalances { get; }

    // Signs in (if needed) and syncs the clock. Safe to call again.
    void Connect(Action<BackendResult> done);

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
    void GetPlayerState(Action<PlayerStateResponse> done);
    void Pull(PullRequest request, Action<PullResponse> done);
    void BuyLetter(BuyLetterRequest request, Action<BuyLetterResponse> done);

    // Pays the letters a finished quest gives (only used when ServerOwnsBalances: otherwise the game adds them itself)
    void ClaimQuest(ClaimQuestRequest request, Action<ClaimQuestResponse> done);

    // Testing tools (only development servers accept them)
    void DevAction(DevRequest request, Action<DevResponse> done);
}
