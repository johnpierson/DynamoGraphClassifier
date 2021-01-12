#
#  -------------------------------------------------------------
#   Copyright (c) Microsoft Corporation.  All rights reserved.
#  -------------------------------------------------------------
"""
Skeleton code showing how to load and run the ONNX export package from Lobe.
"""

import argparse
import json
import os

import numpy as np
from PIL import Image
import onnxruntime as rt


def get_model_and_sig(model_dir):
    """Method to get name of model file. Assumes model is in the parent directory for script."""
    with open(os.path.join(model_dir, "../signature.json"), "r") as f:
        signature = json.load(f)
    model_file = "../" + signature.get("filename")
    if not os.path.isfile(model_file):
        raise FileNotFoundError(f"Model file does not exist")
    return model_file, signature


def load_model(model_file):
    """Load the model from path to model file"""
    # Load ONNX model as session.
    return rt.InferenceSession(path_or_bytes=model_file)


def get_prediction(image, session, signature):
    """
    Predict with the ONNX session!
    """
    # get the signature for model inputs and outputs
    signature_inputs = signature.get("inputs")
    signature_outputs = signature.get("outputs")

    if "Image" not in signature_inputs:
        raise ValueError("ONNX model doesn't have 'Image' input! Check signature.json, and please report issue to Lobe.")

    # process image to be compatible with the model
    img = process_image(image, signature_inputs.get("Image").get("shape"))

    # run the model!
    fetches = [(key, value.get("name")) for key, value in signature_outputs.items()]
    # make the image a batch of 1
    feed = {signature_inputs.get("Image").get("name"): [img]}
    outputs = session.run(output_names=[name for (_, name) in fetches], input_feed=feed)
    # un-batch since we ran an image with batch size of 1,
    # convert to normal python types with tolist(), and convert any byte strings to normal strings with .decode()
    results = {}
    for i, (key, _) in enumerate(fetches):
        val = outputs[i].tolist()[0]
        if isinstance(val, bytes):
            val = val.decode()
        results[key] = val

    return results


def process_image(image, input_shape):
    """
    Given a PIL Image, center square crop and resize to fit the expected model input, and convert from [0,255] to [0,1] values.
    """
    width, height = image.size
    # ensure image type is compatible with model and convert if not
    if image.mode != "RGB":
        image = image.convert("RGB")
    # center crop image (you can substitute any other method to make a square image, such as just resizing or padding edges with 0)
    if width != height:
        square_size = min(width, height)
        left = (width - square_size) / 2
        top = (height - square_size) / 2
        right = (width + square_size) / 2
        bottom = (height + square_size) / 2
        # Crop the center of the image
        image = image.crop((left, top, right, bottom))
    # now the image is square, resize it to be the right shape for the model input
    input_width, input_height = input_shape[1:3]
    if image.width != input_width or image.height != input_height:
        image = image.resize((input_width, input_height))

    # make 0-1 float instead of 0-255 int (that PIL Image loads by default)
    image = np.asarray(image) / 255.0
    # format input as model expects
    return image.astype(np.float32)


def main(image, model_dir):
    """
    Load the model and signature files, start the ONNX session, and run prediction on the image.
    Output prediction will be a dictionary with the same keys as the outputs in the signature.json file.
    """
    model_file, signature = get_model_and_sig(model_dir)
    session = load_model(model_file)
    prediction = get_prediction(image, session, signature)
    return prediction


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Predict a label for an image.")
    parser.add_argument("image", help="Path to your image file.")
    args = parser.parse_args()
    if os.path.isfile(args.image):
        image = Image.open(args.image)
        # convert to rgb image if this isn't one
        if image.mode != "RGB":
            image = image.convert("RGB")
        # Assume model is in the parent directory for this file
        model_dir = os.getcwd()
        print(main(image, model_dir))
    else:
        print(f"Couldn't find image file {args.image}")
