# Upload and Validate checksum service

## Overview

This repository contains two services for handling file uploads to Azure Blob Storage and verifying their integrity through checksums.

## UploadService

The `UploadService` is a background service that uploads files to a specified blob container and computes their SHA1 checksum.

### Features:
- Uploads files to Azure Blob Storage.
- Computes and uploads SHA1 checksum for file validation.

## ChecksumService

The `ChecksumService` downloads blobs, computes their checksums, and validates them against provided checksum files.

### Features:
- Computes checksums for files in a blob container.
- Validates checksums against expected values.
- Optionally validates file listings within TAR archives.

## Getting Started

To use these services, initialize them with the appropriate blob container names and file paths. They will handle file management and cleanup automatically.

## Dependencies

- Azure.Storage.Blobs
- System.Security.Cryptography
- Serilog (for logging)
- SharpCompress (for file listing validation within TAR archives)


# API Endpoints Documentation

## Validate Checksum Endpoint

### POST /validatechecksumbg

This endpoint initiates checksum validation for files stored in Azure Blob Storage.

#### Request:
- Content-Type: `multipart/form-data`
- Parameters: 
  - `filename`: The name of the file in the blob container.
  - `checksumfilename`: The name of the file containing the expected checksum value.
  - `filelistingname`: The name of the file containing the expected file listing (optional).
  - `containername`: The name of the Azure Blob Storage container.

#### Response:
- `200 OK`: Checksum validation started.
- `400 Bad Request`: Unsupported content type or other client error.

#### Logs:
- Information logs for the start and end of the validation process.

---

## File Upload Endpoint

### POST /upload

This endpoint handles the uploading of files to Azure Blob Storage in the background.

#### Request:
- Content-Type: `multipart/form-data`
- Parameters:
  - `file`: The file to be uploaded.
  - `containername`: The name of the target Azure Blob Storage container.

#### Response:
- `200 OK`: File upload initiated in the background.
- `400 Bad Request`: No files received or unsupported content type.

#### Behavior:
- The uploaded file is temporarily stored on the server.
- The background service is provided with the path to the temporary file and the container name.

#### Logs:
- Information logs for the start of the upload process and any errors encountered.

