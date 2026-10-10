using UnityEngine;
using c = MachineSimulator.Constants;

namespace MachineSimulator.ImageProcessing
{
    // NOTE: Both conversions share the same conventions:
    //       - positionInImage is a ball position as reported by BallDetection, i.e. measured from the image
    //         centre with flipped axes (PositionX = width / 2 - column, PositionY = height / 2 - row).
    //       - The camera is mounted 90deg rotated, so the image's short axis (rows) spans the horizontal field
    //         of view and the long axis (columns) spans the vertical one.
    //       - The horizontal direction is flipped on top of that because the image data arrives mirrored.
    //       - A positive vertical angle tilts the viewing ray downwards (Quaternion.Euler(vertical, horizontal, 0)).
    public static class Converter
    {
        // NOTE: Linear-angle model: the viewing angle is proportional to the pixel offset. Compared to the
        //       pinhole model below it is exact at the image centre and at the image borders and drifts in
        //       between (the drift grows with the field of view).
        public static (float HorizontalAngle, float verticalAngle) ConvertToAngle(Vector2 positionInImage)
        {
            var halfWidth = c.CameraResolutionWidth / 2f;
            var halfHeight = c.CameraResolutionHeight / 2f;

            // NOTE: Camera is mounted 90deg offset, so the vertical axis is the wide one (that's why we use width)
            var horizontalAngle = (positionInImage.y / halfHeight) * (c.CameraHorizontalFov / 2f);
            var verticalAngle = (positionInImage.x / halfWidth) * (c.CameraVerticalFov / 2f);

            // NOTE: Need to flip horizontal angle because image data arrives flipped
            return (-horizontalAngle, verticalAngle);
        }

        // NOTE: Pinhole model: the tangent of the viewing angle is proportional to the pixel offset, which is
        //       what a rectilinear lens does. Returns the point (camera space: x right, y up, z forward) where
        //       the viewing ray of positionInImage pierces the image plane `distance` in front of the camera.
        //       The whole image maps onto a plane of 2 * distance * tan(fov / 2) per axis, which is exactly the
        //       quad CameraImagePlaneView draws.
        public static Vector3 ConvertToImagePlanePoint(Vector2 positionInImage, float distance)
        {
            var halfWidth = c.CameraResolutionWidth / 2f;
            var halfHeight = c.CameraResolutionHeight / 2f;
            var tanHalfHorizontalFov = Mathf.Tan(c.CameraHorizontalFov / 2f * Mathf.Deg2Rad);
            var tanHalfVerticalFov = Mathf.Tan(c.CameraVerticalFov / 2f * Mathf.Deg2Rad);

            // NOTE: Same axis mapping and signs as ConvertToAngle (rows -> horizontal, flipped; columns -> vertical, down).
            var right = -(positionInImage.y / halfHeight) * tanHalfHorizontalFov;
            var up = -(positionInImage.x / halfWidth) * tanHalfVerticalFov;

            return new Vector3(right, up, 1f) * distance;
        }

        public static Vector3 ConvertToViewDirection(Vector2 positionInImage)
        {
            return ConvertToImagePlanePoint(positionInImage, 1f).normalized;
        }
    }
}
