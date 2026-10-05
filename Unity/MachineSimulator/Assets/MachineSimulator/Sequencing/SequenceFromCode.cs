using System.Threading;
using Cysharp.Threading.Tasks;
using MachineSimulator.Machine;
using MachineSimulator.MachineModel;
using UnityEngine;

namespace MachineSimulator.Sequencing
{
    public static class SequenceFromCode
    {
        public static void UpDownSequence(MachineModel.MachineModel machineModel, SequenceCreator sequenceCreator, float commandTime)
        {
            var rotation = Quaternion.identity;
            var position = Vector3.up * 0.3f;
            machineModel.HexaPlateMover.UpdatePositionAndRotationTo(position, rotation);
            sequenceCreator.Add(HLInstructionFromCurrentMachineState(machineModel, commandTime));
        }

        public static void UpIntoCircleMovementSequence(MachineModel.MachineModel machineModel, SequenceCreator sequenceCreator, float commandTime)
        {
            var startRotation = Quaternion.identity;
            var startPosition = new Vector3(0f, 0.26f, 0.025f);

            // Go to startPosition
            machineModel.HexaPlateMover.UpdatePositionAndRotationTo(startPosition, startRotation);
            sequenceCreator.Add(HLInstructionFromCurrentMachineState(machineModel, commandTime));

            // circle strategy
            sequenceCreator.Add(new AbstractStrategyInstruction(StrategyName.MoveInCircle, 0f, Mathf.PI * 2f, commandTime, false));
        }

        public static void UpIntoTiltCircleMovementSequence(MachineModel.MachineModel machineModel, SequenceCreator sequenceCreator, float commandTime)
        {
            var startRotation = Quaternion.Euler(0f, 0f, 15f);
            var startPosition = new Vector3(0f, 0.21f, 0f);

            // Go to startPosition
            machineModel.HexaPlateMover.UpdatePositionAndRotationTo(startPosition, startRotation);
            sequenceCreator.Add(HLInstructionFromCurrentMachineState(machineModel, commandTime));

            // circle strategy
            sequenceCreator.Add(new AbstractStrategyInstruction(StrategyName.CircleTilt, 0f, Mathf.PI * 2f, commandTime, false));
        }

        public static async UniTaskVoid StartAsyncExecutionAsync(
            MachineModel.MachineModel machineModel,
            SequenceCreator sequenceCreator,
            float commandTime,
            CancellationToken ct,
            bool executeOnRealMachine = false)
        {
            await ShowOffMultipleMovesInOrder(machineModel, sequenceCreator, commandTime, ct, executeOnRealMachine);
            // await GoUpAndDownForeverAsync(machineModel, sequenceCreator, commandTime, ct, executeOnRealMachine);
            // await GoUpAndDownAsync(machineModel, sequenceCreator, commandTime, ct, executeOnRealMachine, 0f, 0f, 0.04f, 0f);
        }

        private static bool IsDefaultPose(MachineModel.MachineModel machineModel, Vector3 position, Quaternion rotation)
        {
            var (defaultPos, defaultRot) = machineModel.HexaPlateMover.GetDefaultHeightPositionAndRotation();
            return position.Equals(defaultPos) && rotation.Equals(defaultRot);
        }

        private static async UniTask CreateAndPlayFromToSequence(
            SequenceCreator sequenceCreator,
            MachineModel.MachineModel machineModel,
            Vector3 fromPos,
            Quaternion fromRot,
            Vector3 toPos,
            Quaternion toRot,
            float commandTime,
            bool executeOnRealMachine,
            CancellationToken ct)
        {
            var commandTimeInMs = Mathf.RoundToInt(commandTime * 1000f);
            var isStartingFromOriginPose = IsDefaultPose(machineModel, fromPos, fromRot);

            sequenceCreator.ClearAll();

            // NOTE: Set machine state to TO state
            machineModel.HexaPlateMover.UpdatePositionAndRotationTo(toPos, toRot);

            // NOTE: Read back machine state at TO state
            sequenceCreator.Add(HLInstructionFromCurrentMachineState(machineModel, commandTime));

            // NOTE: Start playback from FROM to TO state
            sequenceCreator.StartStringedPlayback(
                executeOnRealMachine,
                resetMachineToStartStateAction: () => machineModel.HexaPlateMover.UpdatePositionAndRotationTo(fromPos, fromRot, isTeleportToOriginPoseChange: isStartingFromOriginPose)
            );

            // NOTE: Wait for the playback to finish
            await UniTask.Delay(commandTimeInMs + 1, cancellationToken: ct);
        }

        private static async UniTask CreateAndPlayFromToStrategySequence(
            SequenceCreator sequenceCreator,
            MachineModel.MachineModel machineModel,
            Vector3 fromPos,
            Quaternion fromRot,
            StrategyName strategyName,
            float startTime,
            float endTime,
            float commandTime,
            bool executeOnRealMachine,
            CancellationToken ct)
        {
            var isStartingFromOriginPose = IsDefaultPose(machineModel, fromPos, fromRot);

            sequenceCreator.ClearAll();

            var duration = endTime - startTime;
            var timeMultiplier = commandTime / 3f;
            var waitDelayInMs = Mathf.RoundToInt(duration * timeMultiplier * 1000f);

            // NOTE: Add strategyInstruction
            sequenceCreator.Add(new AbstractStrategyInstruction(strategyName, startTime, endTime, commandTime, false));

            sequenceCreator.StartStringedPlayback(
                executeOnRealMachine,
                resetMachineToStartStateAction: () => machineModel.HexaPlateMover.UpdatePositionAndRotationTo(fromPos, fromRot, isTeleportToOriginPoseChange: isStartingFromOriginPose)
            );

            await UniTask.Delay(waitDelayInMs + 1, cancellationToken: ct);
        }

        public static async UniTask ThrowBall(
            MachineModel.MachineModel machineModel,
            SequenceCreator sequenceCreator,
            float commandTime,
            CancellationToken ct,
            bool executeOnRealMachine)
        {
            // Default height pose
            var (defaultPosition, defaultRotation) = machineModel.HexaPlateMover.GetDefaultHeightPositionAndRotation();

            var upRotationOne = Quaternion.Euler(10f, 0f, 0f);
            var upRotationTwo = Quaternion.Euler(-10f, 0f, 0f);
            var upPositionOne = new Vector3(0f, 0.20f, 0f);
            var upPositionTwo = new Vector3(0f, 0.23f, 0f);

            // Move to Start pos
            await CreateAndPlayFromToSequence(
                sequenceCreator,
                machineModel,
                fromPos: defaultPosition,
                fromRot: defaultRotation,
                toPos: upPositionOne,
                toRot: upRotationOne,
                commandTime,
                executeOnRealMachine,
                ct
            );

            // To throw end pos
            await CreateAndPlayFromToSequence(
                sequenceCreator,
                machineModel,
                fromPos: upPositionOne,
                fromRot: upRotationOne,
                toPos: upPositionTwo,
                toRot: upRotationTwo,
                commandTime / 5f,
                executeOnRealMachine,
                ct
            );

            // Back to origin
            await CreateAndPlayFromToSequence(
                sequenceCreator,
                machineModel,
                fromPos: upPositionTwo,
                fromRot: upRotationTwo,
                toPos: defaultPosition,
                toRot: defaultRotation,
                commandTime,
                executeOnRealMachine,
                ct
            );
        }


        // 1. Go up and down
        // 2. Tilt in circle
        // 3. Move in circle
        public static async UniTask ShowOffMultipleMovesInOrder(
            MachineModel.MachineModel machineModel,
            SequenceCreator sequenceCreator,
            float commandTime,
            CancellationToken ct,
            bool executeOnRealMachine)
        {
            // Default height pose
            var (defaultPosition, defaultRotation) = machineModel.HexaPlateMover.GetDefaultHeightPositionAndRotation();

            // 1. Go up and down
            {
                var upPosition = new Vector3(0f, 0.22f, 0f);
                var upRotation = Quaternion.Euler(0f, 0f, 0f);

                // From default to up
                await CreateAndPlayFromToSequence(
                    sequenceCreator,
                    machineModel,
                    fromPos: defaultPosition,
                    fromRot: defaultRotation,
                    toPos: upPosition,
                    toRot: upRotation,
                    commandTime,
                    executeOnRealMachine,
                    ct
                );

                // From up to default
                await CreateAndPlayFromToSequence(
                    sequenceCreator,
                    machineModel,
                    fromPos: upPosition,
                    fromRot: upRotation,
                    toPos: defaultPosition,
                    toRot: defaultRotation,
                    commandTime,
                    executeOnRealMachine,
                    ct
                );
            }
            // 2. Tilt in circle
            {
                var startRotation = Quaternion.Euler(0f, 0f, 15f);
                var startPosition = new Vector3(0f, 0.21f, 0f);

                // From default to startPosition
                await CreateAndPlayFromToSequence(
                    sequenceCreator,
                    machineModel,
                    fromPos: defaultPosition,
                    fromRot: defaultRotation,
                    toPos: startPosition,
                    toRot: startRotation,
                    commandTime,
                    executeOnRealMachine,
                    ct
                );

                var startTime = 0f;
                var endTime = Mathf.PI * 2f;

                // Circle Tilt
                await CreateAndPlayFromToStrategySequence(
                    sequenceCreator,
                    machineModel,
                    fromPos: startPosition,
                    fromRot: startRotation,
                    StrategyName.CircleTilt,
                    startTime,
                    endTime,
                    commandTime,
                    executeOnRealMachine,
                    ct
                );

                // Back to origin
                await CreateAndPlayFromToSequence(
                    sequenceCreator,
                    machineModel,
                    fromPos: startPosition,
                    fromRot: startRotation,
                    toPos: defaultPosition,
                    toRot: defaultRotation,
                    commandTime,
                    executeOnRealMachine,
                    ct
                );
            }
            // 3. Move in circle
            {
                var startRotation = Quaternion.identity;
                var startPosition = new Vector3(0f, 0.26f, 0.025f);

                // Go to startPosition
                await CreateAndPlayFromToSequence(
                    sequenceCreator,
                    machineModel,
                    fromPos: defaultPosition,
                    fromRot: defaultRotation,
                    toPos: startPosition,
                    toRot: startRotation,
                    commandTime,
                    executeOnRealMachine,
                    ct
                );

                var startTime = 0f;
                var endTime = Mathf.PI * 2f;

                // Circle Move
                await CreateAndPlayFromToStrategySequence(
                    sequenceCreator,
                    machineModel,
                    fromPos: startPosition,
                    fromRot: startRotation,
                    StrategyName.MoveInCircle,
                    startTime,
                    endTime,
                    commandTime,
                    executeOnRealMachine,
                    ct
                );

                // Back to origin
                await CreateAndPlayFromToSequence(
                    sequenceCreator,
                    machineModel,
                    fromPos: startPosition,
                    fromRot: startRotation,
                    toPos: defaultPosition,
                    toRot: defaultRotation,
                    commandTime,
                    executeOnRealMachine,
                    ct
                );
            }
            // 4. Go up and down with rotation around Y axis
            {
                var upRotation = Quaternion.Euler(0f, 15f, 0f);
                var secondUpRotation = Quaternion.Euler(0f, -15f, 0f);
                var upPosition = new Vector3(0f, 0.22f, 0f);

                // Move up
                await CreateAndPlayFromToSequence(
                    sequenceCreator,
                    machineModel,
                    fromPos: defaultPosition,
                    fromRot: defaultRotation,
                    toPos: upPosition,
                    toRot: upRotation,
                    commandTime,
                    executeOnRealMachine,
                    ct
                );

                // To secondUpRotation
                await CreateAndPlayFromToSequence(
                    sequenceCreator,
                    machineModel,
                    fromPos: upPosition,
                    fromRot: upRotation,
                    toPos: upPosition,
                    toRot: secondUpRotation,
                    commandTime,
                    executeOnRealMachine,
                    ct
                );

                // Back to origin
                await CreateAndPlayFromToSequence(
                    sequenceCreator,
                    machineModel,
                    fromPos: upPosition,
                    fromRot: secondUpRotation,
                    toPos: defaultPosition,
                    toRot: defaultRotation,
                    commandTime,
                    executeOnRealMachine,
                    ct
                );
            }
        }

        // NOTE: command time and height cranked up near to limit (after pressing SpeedX3 button) to optimize for bounce ball height
        // NOTE: We verified that bouncing works with the below code
        private static async UniTask GoUpAndDownForeverAsync(MachineModel.MachineModel machineModel, SequenceCreator sequenceCreator, float commandTime, CancellationToken ct, bool executeOnRealMachine)
        {
            commandTime *= 0.2f;
            var commandTimeInMs = Mathf.RoundToInt(commandTime * 1000f);

            while (true)
            {
                // move up
                sequenceCreator.ClearAll();
                var upRotation = Quaternion.Euler(0f, 0f, 0f);
                var upPosition = new Vector3(0f, 0.2f, 0f);
                machineModel.HexaPlateMover.UpdatePositionAndRotationTo(upPosition, upRotation);
                sequenceCreator.Add(HLInstructionFromCurrentMachineState(machineModel, commandTime));

                sequenceCreator.StartStringedPlayback(executeOnRealMachine, resetMachineToStartStateAction: () => machineModel.HexaPlateMover.TeleportToDefaultHeight());

                await UniTask.Delay(commandTimeInMs + 1, cancellationToken: ct);

                // go back to origin
                sequenceCreator.ClearAll();
                machineModel.HexaPlateMover.TeleportToDefaultHeight();
                sequenceCreator.Add(HLInstructionFromCurrentMachineState(machineModel, commandTime));

                sequenceCreator.StartStringedPlayback(executeOnRealMachine, resetMachineToStartStateAction: () => machineModel.HexaPlateMover.UpdatePositionAndRotationTo(upPosition, upRotation));

                await UniTask.Delay(commandTimeInMs + 1, cancellationToken: ct);
            }
        }

        // NOTE: command time and height cranked up near to limit (after pressing SpeedX3 button) to optimize for bounce ball height
        // NOTE: We verified that bouncing works with the below code
        public static async UniTask GoUpAndDownAsync(
            MachineModel.MachineModel machineModel,
            SequenceCreator sequenceCreator,
            float commandTime,
            CancellationToken ct,
            bool executeOnRealMachine,
            float zTiltAngle,
            float xTiltAngle,
            float upHeightOffset,
            float restHeightIncrease,
            float xOffset,
            float zOffset,
            float yRotationAngle)
        {
            var commandTimeInMs = Mathf.RoundToInt(commandTime * 1000f);

            // move up
            sequenceCreator.ClearAll();
            var upRotation = Quaternion.Euler(xTiltAngle, yRotationAngle, zTiltAngle);
            var upPosition = machineModel.HexaPlateMover.RestPosition + Vector3.up * upHeightOffset;
            machineModel.HexaPlateMover.UpdatePositionAndRotationTo(upPosition, upRotation);
            sequenceCreator.Add(HLInstructionFromCurrentMachineState(machineModel, commandTime));

            sequenceCreator.StartStringedPlayback(executeOnRealMachine, resetMachineToStartStateAction: () => machineModel.HexaPlateMover.TeleportToRestPose());

            await UniTask.Delay(commandTimeInMs + 1, cancellationToken: ct);

            // go back down
            sequenceCreator.ClearAll();

            var newRestPosY = machineModel.HexaPlateMover.RestPosition.y + restHeightIncrease;
            var newRestPosition = new Vector3(xOffset, newRestPosY, zOffset);
            var newRestRotation = Quaternion.identity;
            machineModel.HexaPlateMover.UpdatePositionAndRotationTo(newRestPosition, newRestRotation);
            sequenceCreator.Add(HLInstructionFromCurrentMachineState(machineModel, commandTime));

            sequenceCreator.StartStringedPlayback(executeOnRealMachine, resetMachineToStartStateAction: () => machineModel.HexaPlateMover.UpdatePositionAndRotationTo(upPosition, upRotation));

            await UniTask.Delay(commandTimeInMs + 1, cancellationToken: ct);

            // Update rest position/rotation
            machineModel.HexaPlateMover.UpdateRestPose(newRestPosition, newRestRotation);
        }

        private static async UniTask TiltCircleForeverAsync(MachineModel.MachineModel machineModel, SequenceCreator sequenceCreator, float commandTime, CancellationToken ct, bool executeOnRealMachine)
        {
            var commandTimeInMs = Mathf.RoundToInt(commandTime * 1000f);

            // Go to startPosition
            sequenceCreator.ClearAll();
            var startRotation = Quaternion.Euler(0f, 0f, 15f);
            var startPosition = new Vector3(0f, 0.21f, 0f);

            machineModel.HexaPlateMover.UpdatePositionAndRotationTo(startPosition, startRotation);
            sequenceCreator.Add(HLInstructionFromCurrentMachineState(machineModel, commandTime));

            sequenceCreator.StartStringedPlayback(executeOnRealMachine, resetMachineToStartStateAction: () => machineModel.HexaPlateMover.TeleportToDefaultHeight());
            await UniTask.Delay(commandTimeInMs + 1, cancellationToken: ct);

            var startTime = 0f;
            var endTime = Mathf.PI * 2f;
            var duration = endTime - startTime;
            var timeMultiplier = commandTime / 3f;
            var tiltWaitDelay = Mathf.RoundToInt(duration * timeMultiplier * 1000f);

            while (true)
            {
                // circle strategy
                sequenceCreator.ClearAll();
                sequenceCreator.Add(new AbstractStrategyInstruction(StrategyName.CircleTilt, startTime, endTime, commandTime, false));

                sequenceCreator.StartStringedPlayback(executeOnRealMachine, resetMachineToStartStateAction: () => machineModel.HexaPlateMover.UpdatePositionAndRotationTo(startPosition, startRotation));

                await UniTask.Delay(tiltWaitDelay + 1, cancellationToken: ct);
            }
        }

        private static async UniTask MoveCircleForeverAsync(MachineModel.MachineModel machineModel, SequenceCreator sequenceCreator, float commandTime, CancellationToken ct, bool executeOnRealMachine)
        {
            var commandTimeInMs = Mathf.RoundToInt(commandTime * 1000f);

            // Go to startPosition
            sequenceCreator.ClearAll();
            var startRotation = Quaternion.identity;
            var startPosition = new Vector3(0f, 0.26f, 0.025f);


            machineModel.HexaPlateMover.UpdatePositionAndRotationTo(startPosition, startRotation);
            sequenceCreator.Add(HLInstructionFromCurrentMachineState(machineModel, commandTime));

            sequenceCreator.StartStringedPlayback(executeOnRealMachine, resetMachineToStartStateAction: () => machineModel.HexaPlateMover.TeleportToDefaultHeight());
            await UniTask.Delay(commandTimeInMs + 1, cancellationToken: ct);

            var startTime = 0f;
            var endTime = Mathf.PI * 2f;
            var duration = endTime - startTime;
            var timeMultiplier = commandTime / 3f;
            var tiltWaitDelay = Mathf.RoundToInt(duration * timeMultiplier * 1000f);

            while (true)
            {
                // circle strategy
                sequenceCreator.ClearAll();
                sequenceCreator.Add(new AbstractStrategyInstruction(StrategyName.MoveInCircle, startTime, endTime, commandTime, false));

                sequenceCreator.StartStringedPlayback(executeOnRealMachine, resetMachineToStartStateAction: () => machineModel.HexaPlateMover.UpdatePositionAndRotationTo(startPosition, startRotation));

                await UniTask.Delay(tiltWaitDelay + 1, cancellationToken: ct);
            }
        }

        public static HLInstruction HLInstructionFromCurrentMachineState(MachineModel.MachineModel machineModel, float commandTime)
        {
            var platePosition = machineModel.HexaPlateTransform.position;
            var plateRotation = machineModel.HexaPlateTransform.rotation;
            var hlMachineState = new HLMachineState(platePosition, plateRotation);
            var instruction = new HLInstruction(hlMachineState, commandTime);
            return instruction;
        }
    }
}