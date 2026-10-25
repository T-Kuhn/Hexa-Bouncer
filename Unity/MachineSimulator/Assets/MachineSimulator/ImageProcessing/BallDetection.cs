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
        private List<(Vector2Int center, float radius, int outlinePixelCount, float radialDeviation)> _detectedObjects;
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
            _detectedObjects = new List<(Vector2Int center, float radius, int outlinePixelCount, float radialDeviation)>();
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

        // NOTE: Expected number of traced outline pixels per pixel of radius for a round object, used for
        //       BallRadiusAndPosition.OutlineLengthRoundness. The outline is traced through 8-connected neighbours, so a
        //       diagonal step covers sqrt(2) px of circumference but adds only one pixel: a piece of outline at
        //       angle a (0..45 deg to the nearer axis) holds cos(a) pixels per pixel of length. A circle's
        //       tangent angle is uniformly distributed, which averages to 2*sqrt(2)/PI pixels per pixel of
        //       circumference, so a circle of radius r has 2*PI*r * 2*sqrt(2)/PI = 4*sqrt(2)*r outline pixels.
        //       Verified by running rasterised disks of radius 15..120 px through this very trace: 5.60..5.65
        //       outline pixels per pixel of measured radius, i.e. OutlineLengthRoundness 0.99..1.00 over the whole range.
        private const float OutlinePixelsPerRadiusOfCircle = 5.6568542f;

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

                        var center = new Vector2(
                            accumulatedCenter.x / (float) biggestHalf.Count,
                            accumulatedCenter.y / (float) biggestHalf.Count);
                        var radius = accumulatedRadius / biggestHalf.Count;

                        // NOTE: Mean distance of the outline pixels from the circle just fitted, see
                        //       BallRadiusAndPosition.RadialDeviation. Uses the unrounded centre: rounding it to a
                        //       pixel would alone add up to ~0.3 px of apparent deviation. Mean of absolute values
                        //       rather than RMS, so a reflection notch in the rim stays a small contribution.
                        var radialDeviationSum = 0f;
                        foreach (var boarderPixel in _boarderPixelPositions)
                        {
                            radialDeviationSum += Mathf.Abs(Vector2.Distance(center, boarderPixel) - radius);
                        }

                        // NOTE: _boarderPixelPositions.Count is the number of trace steps around the object,
                        //       i.e. the outline length in pixels (a pixel visited twice around a thin spike
                        //       counts twice, which is intended: such an outline is not round).
                        _detectedObjects.Add(
                            (new Vector2Int(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y)),
                                radius,
                                _boarderPixelPositions.Count,
                                radialDeviationSum / _boarderPixelPositions.Count));

                        break;
                    }
                }
            }

            // NOTE: Biggest first, but every traced object is returned: the caller (UVCCameraPlugin) picks the
            //       ball by CircleDeviation after its radius filter, so a size cap here could drop the ball when
            //       bigger non-ball objects are in the frame.
            var sortedData = _detectedObjects
                .OrderByDescending(data => data.radius)
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
                    PositionY = -data.center.y + c.CameraResolutionHeight / 2f,
                    OutlinePixelCount = data.outlinePixelCount,
                    OutlineLengthRoundness = data.outlinePixelCount / (OutlinePixelsPerRadiusOfCircle * data.radius),
                    RadialDeviation = data.radialDeviation
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

        // NOTE: Number of pixels the traced outline of the object consists of. Only set by
        //       BallDataFromPixelBoarders (BallDataFromArea has no outline, so the shape values below stay 0 there).
        public int OutlinePixelCount;

        // NOTE: First shape measure, sensitive to the boundary texture: OutlinePixelCount divided by the count
        //       a circle of this Radius would have, so 1 is a perfect circle (see
        //       BallDetection.OutlinePixelsPerRadiusOfCircle). Measured on synthetic 1280x720 frames run through
        //       BallDataFromPixelBoarders: plain disk 0.99-1.00, disk with a white reflection notch in its rim
        //       1.02-1.13, disk cut in half by the image edge 0.90, two merged disks 0.91. Below 1 the outline
        //       is too short for the radius: motion blur stretching the ball into an ellipse gives 0.97
        //       (aspect 1.1), 0.93 (1.25), 0.87-0.90 (1.5); rotated rectangles give 0.75-0.81 (the longest
        //       chord runs corner to corner, the sides are short compared to that circle). Above 1 it is too
        //       long: a disk with 8 notches 1.28, ragged rims 1.4-3.2. Blind spot: straight edges within
        //       ~20 deg of the pixel axes are traced with one pixel per pixel of length while a circle averages
        //       0.9, so an axis-aligned square scores 1.06, a square at 20 deg 1.00, a 3:1 axis-aligned
        //       rectangle 0.94 and a triangle 1.02, none of which this measure can tell from a ball.
        public float OutlineLengthRoundness;

        // NOTE: |OutlineLengthRoundness - 1|. Symmetric on purpose: a blurred or edge-clipped ball sits below 1,
        //       a notched or ragged one above.
        public float OutlineLengthDeviation => Mathf.Abs(OutlineLengthRoundness - 1f);

        // NOTE: Second shape measure, sensitive to the silhouette: mean over all outline pixels of
        //       |distance to the centre - Radius|, in pixels. Independent of orientation and of boundary
        //       texture. Measured relative to the radius (RelativeRadialDeviation): plain disk 0.003-0.022
        //       (the ~0.35 px digitisation offset weighs more on small radii), interior reflection 0.01, white
        //       reflection notch in the rim 0.03-0.06, a notch eating half the radius 0.10-0.12, ellipse 0.04
        //       (aspect 1.1), 0.09 (1.25), 0.15 (1.5), square 0.145 at any rotation, triangle 0.21, rounded
        //       square 0.23, 3:1 rectangle 0.29, streak 0.42, two merged disks 0.19, disk cut in half by the
        //       image edge 0.22, disk with 8 notches 0.10. Blind spot: a round-ish patch with a ragged rim
        //       scores 0.02-0.04 like a lightly notched ball, because small wobbles of the rim average out.
        public float RadialDeviation;

        // NOTE: RadialDeviation / Radius, so candidates of different sizes compare.
        public float RelativeRadialDeviation => RadialDeviation / Radius;

        // NOTE: Selection score, 0 is a perfect circle; the candidate with the smallest value is taken as the
        //       ball (UVCCameraPlugin). The two measures are independent and each covers the other's blind
        //       spot: the radial term rejects straight-edged and elongated silhouettes that the length term
        //       rates as round, the length term rejects ragged rims that the radial term averages away.
        //       Measured: plain ball 0.02-0.03, ball with an interior reflection 0.02, small rim notch 0.05,
        //       notch eating half the radius 0.16-0.23, blur aspect 1.1 0.07 and 1.25 0.15, ball cut in half
        //       by the image edge 0.33; square 0.15 (at 20 deg) to 0.39 (45 deg), axis-aligned square 0.21,
        //       3:1 rectangle 0.35, triangle 0.23, disk with 8 notches 0.38, ragged round-ish patch 1.4-2.2.
        //       So a clean or lightly notched ball beats every tested non-ball by 3x, while a heavily notched
        //       or blurred ball is a toss-up against a square and a half-clipped one loses to it.
        public float CircleDeviation => RelativeRadialDeviation + OutlineLengthDeviation;
    }
}
