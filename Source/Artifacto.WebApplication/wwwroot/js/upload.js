// JavaScript interop functions for large file uploads
window.artifactoUpload = {
    uploads: new Map(),
    currentId: 0,

    uploadFile: function (fileInputId, url, dotNetRef, onProgressMethod, onCompleteMethod, onErrorMethod) {
        console.log('=== UPLOAD START ===');
        console.log('URL:', url);
        console.log('File input ID:', fileInputId);
        
        const fileInput = document.getElementById(fileInputId);
        if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
            console.error('No file selected');
            dotNetRef.invokeMethodAsync(onErrorMethod, 'No file selected');
            return null;
        }

        const file = fileInput.files[0];
        console.log('File details:', { name: file.name, size: file.size, type: file.type });
        
        const formData = new FormData();
        formData.append('file', file);

        const xhr = new XMLHttpRequest();
        const uploadId = ++this.currentId;
        
        console.log('Starting upload with ID:', uploadId);
        
        // Store the xhr for cancellation
        this.uploads.set(uploadId, xhr);

        // Track upload progress
        xhr.upload.addEventListener('progress', function (e) {
            if (e.lengthComputable) {
                const percentComplete = (e.loaded / e.total) * 100;
                dotNetRef.invokeMethodAsync(onProgressMethod, e.loaded, e.total, percentComplete);
            }
        });

        // Handle completion
        xhr.addEventListener('load', function () {
            // Clean up
            window.artifactoUpload.uploads.delete(uploadId);
            
            if (xhr.status >= 200 && xhr.status < 300) {
                try {
                    const response = xhr.responseText ? JSON.parse(xhr.responseText) : {};
                    dotNetRef.invokeMethodAsync(onCompleteMethod, response);
                } catch (e) {
                    dotNetRef.invokeMethodAsync(onCompleteMethod, { success: true });
                }
            } else {
                console.error('Upload failed:', xhr.status, xhr.statusText, xhr.responseText);
                let errorMessage = `Upload failed with status ${xhr.status}: ${xhr.statusText}`;
                if (xhr.responseText) {
                    try {
                        const errorResponse = JSON.parse(xhr.responseText);
                        if (errorResponse.message) {
                            errorMessage += ` - ${errorResponse.message}`;
                        }
                    } catch (e) {
                        // If response isn't JSON, include raw text
                        errorMessage += ` - ${xhr.responseText}`;
                    }
                }
                dotNetRef.invokeMethodAsync(onErrorMethod, errorMessage);
            }
        });

        // Handle errors
        xhr.addEventListener('error', function () {
            window.artifactoUpload.uploads.delete(uploadId);
            dotNetRef.invokeMethodAsync(onErrorMethod, 'Network error occurred during upload');
        });

        // Handle abort
        xhr.addEventListener('abort', function () {
            window.artifactoUpload.uploads.delete(uploadId);
            dotNetRef.invokeMethodAsync(onErrorMethod, 'Upload was cancelled');
        });

        // Start the upload
        console.log('Opening connection to:', url);
        xhr.open('POST', url, true);
        console.log('Sending form data...');
        xhr.send(formData);

        // Return upload ID for cancellation
        return uploadId.toString();
    },

    cancelUpload: function (uploadId) {
        const xhr = this.uploads.get(parseInt(uploadId));
        if (xhr) {
            xhr.abort();
            this.uploads.delete(parseInt(uploadId));
        }
    }
};
