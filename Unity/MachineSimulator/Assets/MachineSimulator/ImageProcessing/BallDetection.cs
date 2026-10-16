using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using c = MachineSimulator.Constants;

namespace Unity.MachineSimulator.ImageProcessing
{
    public sealed class BallDetection
    {
        private List<Vector2Int> _positiveProbePoints;
        private List<Vector2Int> _boarderPixelPositions;
        private List<(Vector2Int center, float radius)> _detectedObjects;
        // NOTE: Debug overlay writes recorded during detection and painted afterwards, see BallDataFromPixelBoarders.
        private List<(int index, byte value)> _deferredDebugWrites;

        private Vector2Int[] _cyclePattern1 = new Vector2Int[]
        {
            new Vector2Int(-1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1),
            new Vector2Int(1, 0),
            new Vector2Int(1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, -1),
        };

        private Vector2Int[] _cyclePattern2 = new Vector2Int[]
        {
            new Vector2Int(-1, 1),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1),
            new Vector2Int(1, 0),
            new Vector2Int(1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 0),
        };

        private Vector2Int[] _cyclePattern3 = new Vector2Int[]
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 1),
            new Vector2Int(1, 0),
            new Vector2Int(1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, 1),
        };

        private Vector2Int[] _cyclePattern4 = new Vector2Int[]
        {
            new Vector2Int(1, 1),
            new Vector2Int(1, 0),
            new Vector2Int(1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, 1),
        };

        private Vector2Int[] _cyclePattern5 = new Vector2Int[]
        {
            new Vector2Int(1, 0),
            new Vector2Int(1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1),
        };

        private Vector2Int[] _cyclePattern6 = new Vector2Int[]
        {
            new Vector2Int(1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1),
            new Vector2Int(1, 0),
        };

        private Vector2Int[] _cyclePattern7 = new Vector2Int[]
        {
            new Vector2Int(0, -1),
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1),
            new Vector2Int(1, 0),
            new Vector2Int(1, -1),
        };

        private Vector2Int[] _cyclePattern8 = new Vector2Int[]
        {
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1),
            new Vector2Int(1, 0),
            new Vector2Int(1, -1),
            new Vector2Int(0, -1),
        };

        public BallDetection()
        {
            _positiveProbePoints = new List<Vector2Int>(200);
            _boarderPixelPositions = new List<Vector2Int>(20000);
            _detectedObjects = new List<(Vector2Int center, float radius)>();
            _deferredDebugWrites = new List<(int index, byte value)>(20000);
        }

        // NOTE: BGR bytes. Our ball is orange. Measured on the ball: bright spot R255 G112 B72,
        //       dark spot R109 G22 B16, white-ish highlights never exceed B 130. Over that whole
        //       brightness range orange keeps B/R <= ~0.5 and G/R <= ~0.8, while white/grey wall
        //       pixels have B ~ G ~ R. The ratios are what separate ball from wall; the absolute
        //       bounds just cut off dark background and the brightest neutral pixels.
        //       There is no lower bound on green, so pure red passes too (the dark orange spot is
        //       nearly red, a green minimum would eat the ball's shadow side). The red start line is
        //       the only red in the frame and is drawn after detection for that reason.
        private static bool IsBallPixel(byte[] pixels, int bgrIndex)
        {
            int b = pixels[bgrIndex];
            int g = pixels[bgrIndex + 1];
            int r = pixels[bgrIndex + 2];
            return r >= c.BallMinRed
                   && b <= c.BallMaxBlue
                   && b * 100 <= r * c.BallMaxBluePercentOfRed
                   && g * 100 <= r * c.BallMaxGreenPercentOfRed;
        }

        // NOTE: BGR pixels
        // NOTE: Detection has to read an untouched frame: IsBallPixel looks at all three channels, so a
        //       debug mark painted into a pixel during detection would change later checks on that pixel
        //       (e.g. a green-marked probe pixel inside the ball would read as background when the walk to
        //       the boarder crosses it). All debug writes are therefore recorded in _deferredDebugWrites
        //       and painted once detection is done.
        public List<BallRadiusAndPosition> BallDataFromPixelBoarders(byte[] pixels, int startWidthPixel)
        {
            _positiveProbePoints.Clear();
            _deferredDebugWrites.Clear();

            // we are trying to find the boarders of all orange (ball) pixel clusters.
            for (var height = 0; height < c.CameraResolutionHeight; height += c.PixelSpacing)
            {
                for (var width = startWidthPixel; width < c.CameraResolutionWidth; width += c.PixelSpacing)
                {
                    var index = (height * c.CameraResolutionWidth + width) * 3;

                    if (IsBallPixel(pixels, index))
                    {
                        _deferredDebugWrites.Add((index + 1, 255));
                        // we found a ball-pixel
                        _positiveProbePoints.Add(new Vector2Int(width, height));
                    }
                    else
                    {
                        _deferredDebugWrites.Add((index + 1, 0));
                    }
                }
            }

            _detectedObjects.Clear();
            foreach (var probe in _positiveProbePoints)
            {
                bool isInsideDetectedObject = false;
                foreach (var detectedObject in _detectedObjects)
                {
                    var distance = Vector2Int.Distance(detectedObject.center, probe);

                    if (distance < detectedObject.radius * 1.5f)
                    {
                        isInsideDetectedObject = true;
                    }
                }

                if (isInsideDetectedObject)
                {
                    continue;
                }

                // find boarder pixel
                var offset = -1;
                while (true)
                {
                    var pos = probe + Vector2Int.right * offset;
                    if (!pos.IsInBounds()) break;
                    var idx = pos.GetBGRIndex();
                    if (!IsBallPixel(pixels, idx)) break;
                    offset--;
                }

                offset++;

                var currentPixel = probe + Vector2Int.right * offset;
                var startPixelPosition = currentPixel;
                var lastProbePositionRelativeToCurrentPixel = Vector2Int.left;
                if (currentPixel.IsInBounds())
                {
                    _deferredDebugWrites.Add((currentPixel.GetBGRIndex() + 1, 255));
                }

                _boarderPixelPositions.Clear();

                while (true)
                {
                    var foundPixel = false;
                    var cyclePattern = RotationPatternFromLastProbe(lastProbePositionRelativeToCurrentPixel);

                    for (var i = 0; i < 8; i++)
                    {
                        var pos = currentPixel + cyclePattern[i];
                        if (pos.IsInBounds())
                        {
                            var idx = pos.GetBGRIndex();
                            if (IsBallPixel(pixels, idx))
                            {
                                // we found a new foreground pixel!
                                lastProbePositionRelativeToCurrentPixel = cyclePattern[i - 1] - cyclePattern[i];

                                currentPixel = pos;
                                _deferredDebugWrites.Add((idx + 1, 255));
                                _boarderPixelPositions.Add(currentPixel);

                                foundPixel = true;
                                break;
                            }
                        }
                    }

                    if (!foundPixel)
                    {
                        // it's an island pixel
                        break;
                    }

                    if (currentPixel == startPixelPosition)
                    {
                        // the boarder is complete

                        var dataPoints = new List<(Vector2Int center, float radius)>();
                        var numberOfIterations = 50;
                        for (var i = 0; i < numberOfIterations; i++)
                        {
                            dataPoints.Add(CalculateCenterAndRadius(
                                (int) ((_boarderPixelPositions.Count * i) / (float) numberOfIterations),
                                _boarderPixelPositions));
                        }


                        var biggestHalf = dataPoints
                            .OrderByDescending(dataPoint => dataPoint.radius)
                            .Take(numberOfIterations / 2)
                            .ToList();

                        var accumulatedCenter = new Vector2Int(0, 0);
                        var accumulatedRadius = 0f;
                        foreach (var dataPoint in biggestHalf)
                        {
                            accumulatedCenter += dataPoint.center;
                            accumulatedRadius += dataPoint.radius;
                        }

                        _detectedObjects.Add(
                            (new Vector2Int(
                                    Mathf.RoundToInt(accumulatedCenter.x / (float) biggestHalf.Count),
                                    Mathf.RoundToInt(accumulatedCenter.y / (float) biggestHalf.Count)),
                                accumulatedRadius / biggestHalf.Count));

                        break;
                    }
                }
            }

            var sortedData = _detectedObjects
                .OrderByDescending(data => data.radius)
                .Take(3)
                .ToList();

            // NOTE: Detection is done, from here on we only paint the debug overlay.
            // NOTE: Draw red line to show where the valid area for image processing starts. Drawn after
            //       detection because pure red passes IsBallPixel and the probe scan starts in this column.
            if (startWidthPixel >= 0 && startWidthPixel < c.CameraResolutionWidth)
            {
                for (var height = 0; height < c.CameraResolutionHeight; height++)
                {
                    var index = (height * c.CameraResolutionWidth + startWidthPixel) * 3;
                    pixels[index] = 0;       // B
                    pixels[index + 1] = 0;   // G
                    pixels[index + 2] = 255; // R
                }
            }

            // NOTE: probe hits/misses and traced boarders (green channel)
            foreach (var write in _deferredDebugWrites)
            {
                pixels[write.index] = write.value;
            }

            foreach (var data in sortedData)
            {
                for (int i = 0; i < (int) data.radius; i++)
                {
                    var pos = data.center + Vector2Int.right * i;
                    if (pos.IsInBounds())
                    {
                        pixels[pos.GetBGRIndex() + 1] = 255;
                    }
                }
            }

            return sortedData.Select(data => new BallRadiusAndPosition()
                {
                    Radius = data.radius,
                    PositionX = -data.center.x + c.CameraResolutionWidth / 2f,
                    PositionY = -data.center.y + c.CameraResolutionHeight / 2f
                })
                .ToList();
        }

        private (Vector2Int center, float radius) CalculateCenterAndRadius(int atIndex, List<Vector2Int> boarderPixels)
        {
            var position = boarderPixels[atIndex];
            var maxDistBoarderPixel = new Vector2Int(0, 0);
            var maxDistance = 0f;
            foreach (var boarderPixel in boarderPixels)
            {
                var distance = Vector2Int.Distance(position, boarderPixel);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    maxDistBoarderPixel = boarderPixel;
                }
            }

            var center = new Vector2Int(
                (maxDistBoarderPixel.x + position.x) / 2,
                (maxDistBoarderPixel.y + position.y) / 2);
            var radius = maxDistance / 2;
            
            return (center, radius);
        }

        private Vector2Int[] RotationPatternFromLastProbe(Vector2Int lastProbe)
        {
            if (lastProbe.x == 1)
            {
                // pattern 6: (1, 1)
                if (lastProbe.y == 1)
                    return _cyclePattern4;

                // pattern 4: (1, -1)
                if (lastProbe.y == -1)
                    return _cyclePattern6;

                // pattern 5: (1, 0)
                return _cyclePattern5;
            }

            if (lastProbe.x == -1)
            {
                // pattern 8: (-1, 1)
                if (lastProbe.y == 1)
                    return _cyclePattern2;

                // pattern 2: (-1, -1)
                if (lastProbe.y == -1)
                    return _cyclePattern8;

                // pattern 1: (-1, 0)
                return _cyclePattern1;
            }

            // pattern 7: (0, 1)
            if (lastProbe.y == 1)
                return _cyclePattern3;

            // pattern 3: (0, -1)
                return _cyclePattern7;
        }


        public BallRadiusAndPosition BallDataFromArea(byte[] pixels, int startWidthPixel)
        {
            int numberOfBallPixels = 0;
            var pixelWidth = c.CameraResolutionWidth;
            var accumulatedPixelX = 0;
            var accumulatedPixelY = 0;

            // 1. We are using the orange colour gate (IsBallPixel) to first create our black-and-white base data
            //    0: background
            //    1: foreground
            //    The gate reads all three channels, so a pixel is classified before anything is painted into it.
            for (int i = 0; i < pixels.Length / 3; i++)
            {
                var x = i % pixelWidth;
                if (x < startWidthPixel) continue;

                var idx = i * 3;

                if (IsBallPixel(pixels, idx))
                {
                    accumulatedPixelX += i % pixelWidth;
                    accumulatedPixelY += i / pixelWidth;
                    numberOfBallPixels++;
                    pixels[idx + 2] = 255; // Red
                    pixels[idx + 1] = 100; // Green
                    pixels[idx] = 0;       // Blue
                }
                else
                {
                    pixels[idx + 2] = 0;
                    pixels[idx + 1] = 0;
                    pixels[idx] = 0;
                }
            }

            var meanPixelX = (float) accumulatedPixelX / numberOfBallPixels;
            var meanPixelY = (float) accumulatedPixelY / numberOfBallPixels;

            /*
            // color pixel at ball centre white
            var meanPixelIndex = (int) meanPixelY * c.CameraResolutionWidth + (int) meanPixelX;
            _pixels[meanPixelIndex].r = 1;
            _pixels[meanPixelIndex].g = 1;
            _pixels[meanPixelIndex].b = 1;
            */

            // NOTE: use number of pixels and A_c = r^2 * PI to get the radius (r) of the ball
            var pixelRadius = Mathf.Sqrt(numberOfBallPixels / Mathf.PI);

            return new BallRadiusAndPosition()
            {
                Radius = pixelRadius,
                PositionX = -meanPixelX + c.CameraResolutionWidth / 2f,
                PositionY = -meanPixelY + c.CameraResolutionHeight / 2f
            };
        }
    }
    
    public struct BallRadiusAndPosition
    {
        public float Radius;
        public float PositionX;
        public float PositionY;
    }
}
