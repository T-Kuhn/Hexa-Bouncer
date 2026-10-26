using System;
using System.Collections.Generic;
using MachineSimulator.Controlling;
using MachineSimulator.Machine;
using MachineSimulator.UVCCamera;
using UniRx;
using UnityEngine;

namespace MachineSimulator.UI
{
    public sealed class UiPresenter : MonoBehaviour
    {
        [SerializeField] private UiView _view;
        [SerializeField] private RealMachine _realMachine;
        [SerializeField] private UVCCameraPlugin _uvcOne;
        [SerializeField] private UVCCameraPlugin _uvcTwo;
        [SerializeField] private ModeSwitcher _modeSwitcher;
        [SerializeField] private TiltControlSwitcher _tiltControlSwitcher;
        [SerializeField] private MachineModel.MachineModel _machineModel;

        private float _currentCommandTime = 3f;
        private readonly float _defaultCommandTime = 3f;

        private void Awake()
        {
            var upState = new LLMachineState(-0.93f, 0.93f, -0.93f, 0.93f, -0.93f, 0.93f);
            var originState = new LLMachineState(-0.31f, 0.31f, -0.31f, 0.31f, -0.31f, 0.31f);

            var upOriginManyTimes = new List<LLMachineState>()
            {
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState,
                upState, originState
            };

            Register(_view.OnUpOriginClicked, () => new List<LLMachineState>() { upState, originState }.ToList(_currentCommandTime));
            Register(_view.OnUpOriginManyTimesClicked, () => upOriginManyTimes.ToList(_currentCommandTime));
            Register(_view.OnUpClicked, () => upState.ToList(_currentCommandTime));
            Register(_view.OnOriginClicked, () => originState.ToList(_currentCommandTime));
            Register(_view.OnDownClicked, Constants.OriginMachineState.ToList(_currentCommandTime));

            _view.OnDoubleSpeedClicked
                .Subscribe(_ => { _currentCommandTime = _defaultCommandTime / 2f; })
                .AddTo(this);

            _view.OnQuatrupleSpeedClicked
                .Subscribe(_ => { _currentCommandTime = _defaultCommandTime / 4f; })
                .AddTo(this);

            _view.OnNormalSpeedClicked
                .Subscribe(_ => { _currentCommandTime = _defaultCommandTime; })
                .AddTo(this);


            var amount = 0.01f;
            Register(_view.OnM1PlusClicked, new LLMachineState(-amount, 0f, 0f, 0f, 0f, 0f).ToList(0.1f, true));
            Register(_view.OnM1MinusClicked, new LLMachineState(amount, 0f, 0f, 0f, 0f, 0f).ToList(0.1f, true));
            Register(_view.OnM2PlusClicked, new LLMachineState(0f, amount, 0f, 0f, 0f, 0f).ToList(0.1f, true));
            Register(_view.OnM2MinusClicked, new LLMachineState(0f, -amount, 0f, 0f, 0f, 0f).ToList(0.1f, true));
            Register(_view.OnM3PlusClicked, new LLMachineState(0f, 0f, -amount, 0f, 0f, 0f).ToList(0.1f, true));
            Register(_view.OnM3MinusClicked, new LLMachineState(0f, 0f, amount, 0f, 0f, 0f).ToList(0.1f, true));
            Register(_view.OnM4PlusClicked, new LLMachineState(0f, 0f, 0f, amount, 0f, 0f).ToList(0.1f, true));
            Register(_view.OnM4MinusClicked, new LLMachineState(0f, 0f, 0f, -amount, 0f, 0f).ToList(0.1f, true));
            Register(_view.OnM5PlusClicked, new LLMachineState(0f, 0f, 0f, 0f, -amount, 0f).ToList(0.1f, true));
            Register(_view.OnM5MinusClicked, new LLMachineState(0f, 0f, 0f, 0f, amount, 0f).ToList(0.1f, true));
            Register(_view.OnM6PlusClicked, new LLMachineState(0f, 0f, 0f, 0f, 0f, amount).ToList(0.1f, true));
            Register(_view.OnM6MinusClicked, new LLMachineState(0f, 0f, 0f, 0f, 0f, -amount).ToList(0.1f, true));

            Register(_view.OnApplyOffsetClicked, Constants.OffsetFromTableState.ToList(1f, true));

            _view.OnApplyOffsetClicked.Subscribe(_ => { _realMachine.IsReady = true; }).AddTo(this);
        }

        private void Register(IObservable<Unit> observable, Func<List<LLInstruction>> getInstructions)
        {
            observable
                .Subscribe(_ => _realMachine.Instruct(getInstructions()))
                .AddTo(this);
        }

        private void Register(IObservable<Unit> observable, List<LLInstruction> instructions)
        {
            observable
                .Subscribe(_ => _realMachine.Instruct(instructions))
                .AddTo(this);
        }

        private void Update()
        {
            var countOne = _uvcOne.ImageRetrievalTookTooLongCount;
            var timeOne = _uvcOne.LastImageRetrievalTime;
            _view.SetTextOnCamOneCounterLabelTo($"camOne: {countOne}, {timeOne}ms");
            
            var countTwo = _uvcTwo.ImageRetrievalTookTooLongCount;
            var timeTwo = _uvcTwo.LastImageRetrievalTime;
            _view.SetTextOnCamTwoCounterLabelTo($"camTwo: {countTwo}, {timeTwo}ms");

            _view.SetTextOnCurrentModeLabelTo($"Mode: {_modeSwitcher.CurrentMode}");
            _view.SetTextOnTiltControlLabelTo($"Tilt: {_tiltControlSwitcher.CurrentTiltControl}");

            var restHeight = _machineModel.HexaPlateMover.RestPosition.y;
            _view.SetTextOnRestHeightLabelTo($"RestH: {restHeight:F4} m");
        }
    }
}