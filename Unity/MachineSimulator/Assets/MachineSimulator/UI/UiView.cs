using System;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace MachineSimulator.UI
{
    public sealed class UiView : MonoBehaviour
    {
        private readonly Subject<Unit> _onUpOriginManyTimesClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onUpOriginClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onUpClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onOriginClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onDownClicked = new Subject<Unit>();

        private readonly Subject<Unit> _onNormalSpeedClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onDoubleSpeedClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onQuatrupleSpeedClicked = new Subject<Unit>();

        private readonly Subject<Unit> _onSeqNormalSpeedClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onSeqDoubleSpeedClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onSeqQuatrupleSpeedClicked = new Subject<Unit>();

        private readonly Subject<Unit> _onM1PlusClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onM1MinusClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onM2PlusClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onM2MinusClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onM3PlusClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onM3MinusClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onM4PlusClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onM4MinusClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onM5PlusClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onM5MinusClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onM6PlusClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onM6MinusClicked = new Subject<Unit>();

        private readonly Subject<Unit> _onApplyOffsetClicked = new Subject<Unit>();

        private readonly Subject<Unit> _onLoadSequenceFromCodeClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onAddInstructionClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onTeleportToOriginClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onPlaybackStringedClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onSendStringedToMachineClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onClearAllClicked = new Subject<Unit>();

        private readonly Subject<Unit> _onPlaybackAsyncClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onPlaybackAsyncOnMachineClicked = new Subject<Unit>();
        private readonly Subject<Unit> _onStopAllAsync = new Subject<Unit>();

        private Label _fpsCounterLabel;
        private Label _camOneCounterLabel;
        private Label _camTwoCounterLabel;
        private Label _currentModeLabel;
        private Label _tiltControlLabel;
        private Label _restHeightLabel;

        public IObservable<Unit> OnUpOriginManyTimesClicked => _onUpOriginManyTimesClicked;
        public IObservable<Unit> OnUpOriginClicked => _onUpOriginClicked;
        public IObservable<Unit> OnUpClicked => _onUpClicked;
        public IObservable<Unit> OnDownClicked => _onDownClicked;
        public IObservable<Unit> OnOriginClicked => _onOriginClicked;

        public IObservable<Unit> OnNormalSpeedClicked => _onNormalSpeedClicked;
        public IObservable<Unit> OnDoubleSpeedClicked => _onDoubleSpeedClicked;
        public IObservable<Unit> OnQuatrupleSpeedClicked => _onQuatrupleSpeedClicked;

        public IObservable<Unit> OnSeqNormalSpeedClicked => _onSeqNormalSpeedClicked;
        public IObservable<Unit> OnSeqDoubleSpeedClicked => _onSeqDoubleSpeedClicked;
        public IObservable<Unit> OnSeqQuatrupleSpeedClicked => _onSeqQuatrupleSpeedClicked;

        public IObservable<Unit> OnM1PlusClicked => _onM1PlusClicked;
        public IObservable<Unit> OnM1MinusClicked => _onM1MinusClicked;
        public IObservable<Unit> OnM2PlusClicked => _onM2PlusClicked;
        public IObservable<Unit> OnM2MinusClicked => _onM2MinusClicked;
        public IObservable<Unit> OnM3PlusClicked => _onM3PlusClicked;
        public IObservable<Unit> OnM3MinusClicked => _onM3MinusClicked;
        public IObservable<Unit> OnM4PlusClicked => _onM4PlusClicked;
        public IObservable<Unit> OnM4MinusClicked => _onM4MinusClicked;
        public IObservable<Unit> OnM5PlusClicked => _onM5PlusClicked;
        public IObservable<Unit> OnM5MinusClicked => _onM5MinusClicked;
        public IObservable<Unit> OnM6PlusClicked => _onM6PlusClicked;
        public IObservable<Unit> OnM6MinusClicked => _onM6MinusClicked;

        public IObservable<Unit> OnApplyOffsetClicked => _onApplyOffsetClicked;

        public IObservable<Unit> OnLoadSequenceFromCodeClicked => _onLoadSequenceFromCodeClicked;
        public IObservable<Unit> OnAddInstructionClicked => _onAddInstructionClicked;
        public IObservable<Unit> OnTeleportToOriginClicked => _onTeleportToOriginClicked;
        public IObservable<Unit> OnPlaybackStringedClicked => _onPlaybackStringedClicked;
        public IObservable<Unit> OnSendStringedToMachineClicked => _onSendStringedToMachineClicked;
        public IObservable<Unit> OnClearAllClicked => _onClearAllClicked;

        public IObservable<Unit> OnPlaybackAsyncClicked => _onPlaybackAsyncClicked;
        public IObservable<Unit> OnPlaybackAsyncOnMachineClicked => _onPlaybackAsyncOnMachineClicked;
        public IObservable<Unit> OnStopAllAsyncCLicked => _onStopAllAsync;
        
        public void SetTextOnFpsCounterLabelTo(string text) => _fpsCounterLabel.text = text;
        
        public void SetTextOnCamOneCounterLabelTo(string text) => _camOneCounterLabel.text = text;
        
        public void SetTextOnCamTwoCounterLabelTo(string text) => _camTwoCounterLabel.text = text;
        
        public void SetTextOnCurrentModeLabelTo(string text) => _currentModeLabel.text = text;

        public void SetTextOnTiltControlLabelTo(string text) => _tiltControlLabel.text = text;

        public void SetTextOnRestHeightLabelTo(string text) => _restHeightLabel.text = text;

        private void Awake()
        {
            var uiDocument = GetComponent<UIDocument>();
            var root = uiDocument.rootVisualElement;

            RegisterButton(root.Q<Button>("UpOriginManyTimesButton"), _onUpOriginManyTimesClicked);
            RegisterButton(root.Q<Button>("UpOriginButton"), _onUpOriginClicked);
            RegisterButton(root.Q<Button>("DownButton"), _onDownClicked);
            RegisterButton(root.Q<Button>("OriginButton"), _onOriginClicked);
            RegisterButton(root.Q<Button>("UpButton"), _onUpClicked);

            RegisterButton(root.Q<Button>("DoubleSpeedButton"), _onDoubleSpeedClicked);
            RegisterButton(root.Q<Button>("QuatrupleSpeedButton"), _onQuatrupleSpeedClicked);
            RegisterButton(root.Q<Button>("NormalSpeedButton"), _onNormalSpeedClicked);

            RegisterButton(root.Q<Button>("SeqDoubleSpeedButton"), _onSeqDoubleSpeedClicked);
            RegisterButton(root.Q<Button>("SeqQuatrupleSpeedButton"), _onSeqQuatrupleSpeedClicked);
            RegisterButton(root.Q<Button>("SeqNormalSpeedButton"), _onSeqNormalSpeedClicked);

            RegisterButton(root.Q<VisualElement>("M1").Q<Button>("CalibratePlus"), _onM1PlusClicked);
            RegisterButton(root.Q<VisualElement>("M1").Q<Button>("CalibrateMinus"), _onM1MinusClicked);
            RegisterButton(root.Q<VisualElement>("M2").Q<Button>("CalibratePlus"), _onM2PlusClicked);
            RegisterButton(root.Q<VisualElement>("M2").Q<Button>("CalibrateMinus"), _onM2MinusClicked);
            RegisterButton(root.Q<VisualElement>("M3").Q<Button>("CalibratePlus"), _onM3PlusClicked);
            RegisterButton(root.Q<VisualElement>("M3").Q<Button>("CalibrateMinus"), _onM3MinusClicked);
            RegisterButton(root.Q<VisualElement>("M4").Q<Button>("CalibratePlus"), _onM4PlusClicked);
            RegisterButton(root.Q<VisualElement>("M4").Q<Button>("CalibrateMinus"), _onM4MinusClicked);
            RegisterButton(root.Q<VisualElement>("M5").Q<Button>("CalibratePlus"), _onM5PlusClicked);
            RegisterButton(root.Q<VisualElement>("M5").Q<Button>("CalibrateMinus"), _onM5MinusClicked);
            RegisterButton(root.Q<VisualElement>("M6").Q<Button>("CalibratePlus"), _onM6PlusClicked);
            RegisterButton(root.Q<VisualElement>("M6").Q<Button>("CalibrateMinus"), _onM6MinusClicked);

            RegisterButton(root.Q<Button>("ApplyOffset"), _onApplyOffsetClicked);

            RegisterButton(root.Q<Button>("LoadSequenceFromCodeButton"), _onLoadSequenceFromCodeClicked);
            RegisterButton(root.Q<Button>("AddInstructionButton"), _onAddInstructionClicked);
            RegisterButton(root.Q<Button>("TeleportToOrigin"), _onTeleportToOriginClicked);
            RegisterButton(root.Q<Button>("PlaybackStringedButton"), _onPlaybackStringedClicked);
            RegisterButton(root.Q<Button>("SendStringedToMachine"), _onSendStringedToMachineClicked);
            RegisterButton(root.Q<Button>("ClearAllButton"), _onClearAllClicked);

            RegisterButton(root.Q<Button>("PlaybackAsync"), _onPlaybackAsyncClicked);
            RegisterButton(root.Q<Button>("PlaybackAsyncOnMachine"), _onPlaybackAsyncOnMachineClicked);
            RegisterButton(root.Q<Button>("StopAllAsync"), _onStopAllAsync);

            _fpsCounterLabel = root.Q<Label>("FpsCounterLabel");
            _camOneCounterLabel = root.Q<Label>("CamOneCounterLabel");
            _camTwoCounterLabel = root.Q<Label>("CamTwoCounterLabel");
            _currentModeLabel = root.Q<Label>("CurrentModeLabel");
            _tiltControlLabel = root.Q<Label>("TiltControlLabel");
            _restHeightLabel = root.Q<Label>("RestHeightLabel");
        }

        private void RegisterButton(Button button, Subject<Unit> subject)
        {
            Observable
                .FromEvent(x => button.clicked += x, x => button.clicked -= x)
                .Subscribe(_ => subject.OnNext(Unit.Default))
                .AddTo(this);
        }
    }
}