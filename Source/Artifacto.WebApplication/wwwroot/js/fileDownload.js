window.downloadFileFromStream = async (fileName, contentStreamReference) => {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer]);
    const url = URL.createObjectURL(blob);
    
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? '';
    anchorElement.click();
    anchorElement.remove();
    
    URL.revokeObjectURL(url);
};

window.downloadFileWithProgress = async (fileName, url, dotnetRef) => {
    try {
        const response = await fetch(url);
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        const contentLength = response.headers.get('Content-Length');
        const total = contentLength ? parseInt(contentLength, 10) : 0;
        
        const reader = response.body.getReader();
        const chunks = [];
        let loaded = 0;
        
        while (true) {
            const { done, value } = await reader.read();
            
            if (done) break;
            
            chunks.push(value);
            loaded += value.length;
            
            // Report progress back to Blazor component
            if (total > 0) {
                const progress = Math.round((loaded / total) * 100);
                await dotnetRef.invokeMethodAsync('UpdateProgress', progress);
            }
        }
        
        // Create blob and download
        const blob = new Blob(chunks);
        const blobUrl = URL.createObjectURL(blob);
        
        const anchorElement = document.createElement('a');
        anchorElement.href = blobUrl;
        anchorElement.download = fileName ?? '';
        anchorElement.click();
        anchorElement.remove();
        
        URL.revokeObjectURL(blobUrl);
        
        // Notify completion
        await dotnetRef.invokeMethodAsync('DownloadComplete');
        
    } catch (error) {
        await dotnetRef.invokeMethodAsync('DownloadError', error.message);
    }
};

window.downloadFileFromUrl = (fileName, url) => {
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? '';
    anchorElement.click();
    anchorElement.remove();
};
