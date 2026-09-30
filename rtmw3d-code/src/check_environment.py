from importlib.metadata import version
from pathlib import Path
import sys

import cv2
import mmcv
import mmengine
import mmdet
import mmpose
import numpy as np
import torch
import torch.version
from mmcv.ops import nms


def main() -> None:
    project_root = Path(__file__).resolve().parent.parent

    print("=" * 60)
    print("RTMW3D environment check")
    print("=" * 60)

    print(f"Project root : {project_root}")
    print(f"Python       : {sys.executable}")
    print(f"Python ver.  : {sys.version.split()[0]}")
    print()

    print(f"PyTorch      : {torch.__version__}")
    print(f"Torch CUDA   : {torch.version.cuda}")
    print(f"MMCV         : {mmcv.__version__}")
    print(f"MMEngine     : {mmengine.__version__}")
    print(f"MMDetection  : {mmdet.__version__}")
    print(f"MMPose       : {version('mmpose')}")
    print(f"NumPy        : {np.__version__}")
    print(f"OpenCV       : {cv2.__version__}")
    print()

    print(f"MMPose path  : {mmpose.__file__}")
    print(f"MMCV path    : {mmcv.__file__}")
    print()

    if not torch.cuda.is_available():
        raise RuntimeError("CUDAが利用できません。PyTorchとGPUドライバーを確認してください。")

    print(f"GPU          : {torch.cuda.get_device_name(0)}")
    print(f"Capability   : {torch.cuda.get_device_capability(0)}")

    expected_capability = (12, 0)
    actual_capability = torch.cuda.get_device_capability(0)

    if actual_capability != expected_capability:
        print(
            f"注意: GPU Compute Capabilityは{actual_capability}です。"
            f"想定値は{expected_capability}です。"
        )

    # PyTorch GPU演算
    x = torch.randn(1024, 1024, device="cuda")
    y = x @ x
    torch.cuda.synchronize()

    print(f"GPU matmul   : OK, shape={tuple(y.shape)}")

    # MMCV CUDA NMS
    boxes = torch.tensor(
        [
            [0.0, 0.0, 10.0, 10.0],
            [1.0, 1.0, 11.0, 11.0],
            [20.0, 20.0, 30.0, 30.0],
        ],
        device="cuda",
    )
    scores = torch.tensor([0.9, 0.8, 0.7], device="cuda")

    detections, indices = nms(boxes, scores, iou_threshold=0.5)
    torch.cuda.synchronize()

    print(f"MMCV NMS     : OK, kept indices={indices.tolist()}")
    print()
    print("Environment check completed successfully.")


if __name__ == "__main__":
    main()