using NUnit.Framework;
using Axiom.Battle;

[TestFixture]
public class SpellInputUILogicTests
{
    private SpellInputUILogic _logic;

    [SetUp]
    public void SetUp() => _logic = new SpellInputUILogic();

    // ── Initial state ─────────────────────────────────────────────────────────

    [Test]
    public void InitialState_IsIdle()
    {
        Assert.That(_logic.CurrentState, Is.EqualTo(SpellInputUILogic.State.Idle));
    }

    [Test]
    public void InitialState_RecognizedSpellName_IsNull()
    {
        Assert.That(_logic.RecognizedSpellName, Is.Null);
    }

    // ── ShowPrompt ────────────────────────────────────────────────────────────

    [Test]
    public void ShowPrompt_SetsPromptVisibleState()
    {
        _logic.ShowPrompt();
        Assert.That(_logic.CurrentState, Is.EqualTo(SpellInputUILogic.State.PromptVisible));
    }

    [Test]
    public void ShowPrompt_ClearsRecognizedSpellName()
    {
        _logic.ShowResult("Hydrogen Blast");
        _logic.ShowPrompt();
        Assert.That(_logic.RecognizedSpellName, Is.Null);
    }

    // ── StartListening ────────────────────────────────────────────────────────

    [Test]
    public void StartListening_SetsListeningState()
    {
        _logic.StartListening();
        Assert.That(_logic.CurrentState, Is.EqualTo(SpellInputUILogic.State.Listening));
    }

    [Test]
    public void StartListening_ClearsRecognizedSpellName()
    {
        _logic.ShowResult("Hydrogen Blast");
        _logic.StartListening();
        Assert.That(_logic.RecognizedSpellName, Is.Null);
    }

    // ── ShowResult ────────────────────────────────────────────────────────────

    [Test]
    public void ShowResult_SetsSpellRecognizedState()
    {
        _logic.ShowResult("Hydrogen Blast");
        Assert.That(_logic.CurrentState, Is.EqualTo(SpellInputUILogic.State.SpellRecognized));
    }

    [Test]
    public void ShowResult_StoresSpellName()
    {
        _logic.ShowResult("Hydrogen Blast");
        Assert.That(_logic.RecognizedSpellName, Is.EqualTo("Hydrogen Blast"));
    }

    [Test]
    public void ShowResult_OverwritesPreviousSpellName()
    {
        _logic.ShowResult("Hydrogen Blast");
        _logic.ShowResult("Sodium Surge");
        Assert.That(_logic.RecognizedSpellName, Is.EqualTo("Sodium Surge"));
    }

    // ── ShowError ─────────────────────────────────────────────────────────────

    [Test]
    public void ShowError_SetsNotRecognizedState()
    {
        _logic.ShowError();
        Assert.That(_logic.CurrentState, Is.EqualTo(SpellInputUILogic.State.NotRecognized));
    }

    [Test]
    public void ShowError_ClearsRecognizedSpellName()
    {
        _logic.ShowResult("Hydrogen Blast");
        _logic.ShowError();
        Assert.That(_logic.RecognizedSpellName, Is.Null);
    }

    // ── Hide ──────────────────────────────────────────────────────────────────

    [Test]
    public void Hide_ResetsToIdleState()
    {
        _logic.ShowPrompt();
        _logic.Hide();
        Assert.That(_logic.CurrentState, Is.EqualTo(SpellInputUILogic.State.Idle));
    }

    [Test]
    public void Hide_ClearsRecognizedSpellName()
    {
        _logic.ShowResult("Hydrogen Blast");
        _logic.Hide();
        Assert.That(_logic.RecognizedSpellName, Is.Null);
    }

    // ── State transitions ──────────────────────────────────────────────────────

    [Test]
    public void FullFlow_PromptToListeningToResult()
    {
        _logic.ShowPrompt();
        _logic.StartListening();
        _logic.ShowPrompt(); // PTT released, back to prompt while processing
        _logic.ShowResult("Sodium Surge");

        Assert.That(_logic.CurrentState,        Is.EqualTo(SpellInputUILogic.State.SpellRecognized));
        Assert.That(_logic.RecognizedSpellName, Is.EqualTo("Sodium Surge"));
    }

    [Test]
    public void FullFlow_PromptToListeningToError()
    {
        _logic.ShowPrompt();
        _logic.StartListening();
        _logic.ShowPrompt(); // PTT released
        _logic.ShowError();

        Assert.That(_logic.CurrentState,        Is.EqualTo(SpellInputUILogic.State.NotRecognized));
        Assert.That(_logic.RecognizedSpellName, Is.Null);
    }

    // ── ShowRejection ─────────────────────────────────────────────────────────

    [Test]
    public void ShowRejection_SetsRejectedState()
    {
        _logic.ShowRejection("Not enough MP.");
        Assert.That(_logic.CurrentState, Is.EqualTo(SpellInputUILogic.State.Rejected));
    }

    [Test]
    public void ShowRejection_StoresRejectionMessage()
    {
        _logic.ShowRejection("Not enough MP to cast Freeze.");
        Assert.That(_logic.RejectionMessage, Is.EqualTo("Not enough MP to cast Freeze."));
    }

    [Test]
    public void ShowPrompt_ClearsRejectionMessage()
    {
        _logic.ShowRejection("Not enough MP.");
        _logic.ShowPrompt();
        Assert.That(_logic.RejectionMessage, Is.Null);
    }
}
