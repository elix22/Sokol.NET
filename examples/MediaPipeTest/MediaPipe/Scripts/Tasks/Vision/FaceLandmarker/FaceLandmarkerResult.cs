// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using System.Numerics;

namespace Mediapipe.Tasks.Vision.FaceLandmarker
{
  /// <summary>
  ///   The face landmarks result from FaceLandmarker, where each vector element represents a single face detected in the image.
  /// </summary>
  public readonly struct FaceLandmarkerResult
  {
    /// <summary>
    ///   Detected face landmarks in normalized image coordinates.
    /// </summary>
    public readonly List<NormalizedLandmarks> faceLandmarks;
    /// <summary>
    ///   Optional face blendshapes results.
    /// </summary>
    public readonly List<Classifications> faceBlendshapes;
    /// <summary>
    ///   Optional facial transformation matrix.
    /// </summary>
    public readonly List<Matrix4x4> facialTransformationMatrixes;

    internal FaceLandmarkerResult(List<NormalizedLandmarks> faceLandmarks,
        List<Classifications> faceBlendshapes, List<Matrix4x4> facialTransformationMatrixes)
    {
      this.faceLandmarks = faceLandmarks;
      this.faceBlendshapes = faceBlendshapes;
      this.facialTransformationMatrixes = facialTransformationMatrixes;
    }

    public static FaceLandmarkerResult Alloc(int capacity, bool outputFaceBlendshapes = false, bool outputFaceTransformationMatrixes = false)
    {
      var faceLandmarks = new List<NormalizedLandmarks>(capacity);
      var faceBlendshapes = outputFaceBlendshapes ? new List<Classifications>(capacity) : null;
      var facialTransformationMatrixes = outputFaceTransformationMatrixes ? new List<Matrix4x4>(capacity) : null;
      return new FaceLandmarkerResult(faceLandmarks, faceBlendshapes, facialTransformationMatrixes);
    }

    public void CloneTo(ref FaceLandmarkerResult destination)
    {
      if (faceLandmarks == null)
      {
        destination = default;
        return;
      }

      var dstFaceLandmarks = destination.faceLandmarks ?? new List<NormalizedLandmarks>(faceLandmarks.Count);
      dstFaceLandmarks.CopyFrom(faceLandmarks);

      var dstFaceBlendshapes = destination.faceBlendshapes;
      if (faceBlendshapes != null)
      {
        dstFaceBlendshapes ??= new List<Classifications>(faceBlendshapes.Count);
        dstFaceBlendshapes.CopyFrom(faceBlendshapes);
      }

      var dstFacialTransformationMatrixes = destination.facialTransformationMatrixes;
      if (facialTransformationMatrixes != null)
      {
        dstFacialTransformationMatrixes ??= new List<Matrix4x4>(facialTransformationMatrixes.Count);
        dstFacialTransformationMatrixes.Clear();
        dstFacialTransformationMatrixes.AddRange(facialTransformationMatrixes);
      }

      destination = new FaceLandmarkerResult(dstFaceLandmarks, dstFaceBlendshapes, dstFacialTransformationMatrixes);
    }

    public override string ToString()
      => $"{{ \"faceLandmarks\": {Util.Format(faceLandmarks)}, \"faceBlendshapes\": {Util.Format(faceBlendshapes)}, \"facialTransformationMatrixes\": {Util.Format(facialTransformationMatrixes)} }}";
  }

  internal static class MatrixDataExtension
  {
    public static Matrix4x4 ToMatrix4x4(this MatrixData matrixData)
    {
      var data = matrixData.PackedData;
      // NOTE: z direction is inverted
      if (matrixData.Layout == MatrixData.Types.Layout.RowMajor)
      {
        // Row-major layout: set rows directly using Matrix4x4 fields
        return new Matrix4x4(
          data[0], data[1], -data[2], data[3],      // Row 0
          data[4], data[5], -data[6], data[7],      // Row 1
          -data[8], -data[9], data[10], -data[11],  // Row 2
          data[12], data[13], -data[14], data[15]   // Row 3
        );
      }
      else
      {
        // Column-major layout: transpose while creating
        return new Matrix4x4(
          data[0], data[4], -data[8], data[12],     // Row 0 (from columns)
          data[1], data[5], -data[9], data[13],     // Row 1 (from columns)
          -data[2], -data[6], data[10], -data[14],  // Row 2 (from columns)
          data[3], data[7], -data[11], data[15]     // Row 3 (from columns)
        );
      }
    }
  }
}
