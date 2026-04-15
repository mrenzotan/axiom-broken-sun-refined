using System;

namespace Axiom.Core
{
    /// <summary>
    /// Pure business logic for a generic Yes/No confirmation dialog. No Unity dependencies.
    /// Instantiated per-open by the owning MonoBehaviour (e.g. <c>ConfirmNewGameDialogUI</c>)
    /// with the callbacks that should fire on confirm and cancel.
    /// </summary>
    public sealed class ConfirmDialogController
    {
        private readonly Action _onConfirm;
        private readonly Action _onCancel;

        /// <param name="onConfirm">Invoked on Yes. Required.</param>
        /// <param name="onCancel">Invoked on No / Esc / B-button / background-click. Required.</param>
        public ConfirmDialogController(Action onConfirm, Action onCancel)
        {
            _onConfirm = onConfirm ?? throw new ArgumentNullException(nameof(onConfirm));
            _onCancel  = onCancel  ?? throw new ArgumentNullException(nameof(onCancel));
        }

        public void OnYesClicked() => _onConfirm();
        public void OnNoClicked()  => _onCancel();
    }
}
