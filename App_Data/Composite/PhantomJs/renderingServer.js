function getPlaceholdersLocationInfo(placeholderElementName) {
    var ret = [];

    var placeholders = document.getElementsByTagName(placeholderElementName);
    for (i = 0; pl = placeholders[i]; i++) {
        var clientRect = pl.getBoundingClientRect();
        var placeholderInfoParts = [pl.id.substring(3), clientRect.left, clientRect.top, clientRect.width, clientRect.height];
        ret.push(placeholderInfoParts.join(','));
    }
    return ret.join('|');
};

function BuildFunctionPreview(system, console, address, output, authCookie, mode) {
    var page = require('webpage').create();
    var globalTimeout = null;
    
	if(authCookie != null) {
		phantom.deleteCookie(authCookie.name);
		
		var cookieAdded = phantom.addCookie(authCookie);
		if(!cookieAdded) {
			// Adding the cookie header manually, if PhantomJs fails to add because of a custom domain name used (f.e. "local" instead of "localhost")
			page.customHeaders = { 'Cookie' : authCookie.name + '=' + authCookie.value };
		}
	}	
		
	if (mode == "template") {
	    page.viewportSize = { width: 1920, height: 600 };
	} else {
	    page.viewportSize = { width: 1920, height: 600 };
	}

    page.settings.resourceTimeout = (mode == "test") ? 2000 : 5000;
    
	page.onResourceTimeout = function (request) {
	    if (request.id == 1) {
	        if (globalTimeout != null) {
	            clearTimeout(globalTimeout);
	            globalTimeout = null;
	        }
	        console.log('ERROR, page.onResourceTimeout: ' + JSON.stringify(request.errorString) + ', URL: ' + JSON.stringify(request.url));
	        
            phantom.exit(); // TODO: optimize, no exit needed
	    }
	};

    // if js errors happen on the page 
	page.onError = function (msg, trace) {
        // ignore in page js errors - some dev writing sloppy js, should not affect us
	}

    // redirects ...
	page.onResourceReceived = function (response) {
	    if (response.id == 1 && (response.status == 301 || response.status == 302)) {
	        console.log('REDIRECT: ' + response.url);
	        phantom.exit(); // TODO: optimize, no exit needed
	    }
	}

    // called by our custom js injected in the rendered page
	page.onCallback = function (data) {
	    if (globalTimeout != null) {
	        clearTimeout(globalTimeout);
	        globalTimeout = null;
	    }
	    if (mode == "function") {
	        var previewElementId = "CompositeC1FunctionPreview";

	        var clientRect = page.evaluate("getFunctionPreviewClientRect", previewElementId);

	        if (clientRect != null && clientRect.height > 1 && clientRect.width > 1) {

	            // Limiting image height
	            if (clientRect.height > 800) {
	                clientRect.height = 800;
	            }
	            page.clipRect = clientRect;
	        } else {
	            // Rendering an empty spot
	            page.clipRect = { top: 0, left: 0, height: 1, width: 1 };
	        }

	        page.render(output);
	        page.close();

	        console.log('SUCCESS: ' + address);
	    } else if (mode == "template") {
	        // Template preview:
	        var placeholdersInfo = page.evaluate(getPlaceholdersLocationInfo, 'placeholderpreview');

	        page.render(output);
	        page.close();

	        console.log('templateInfo:' + placeholdersInfo);
	    } else {
	        page.render(output);
	        page.close();

	        console.log('SUCCESS');
	    }

	    WaitForInput(system, console);
	};

    try {
        page.open(address, function (status) {
            if (status !== 'success') {
                if (globalTimeout != null) {
                    clearTimeout(globalTimeout);
                    globalTimeout = null;
                }
                console.log('ERROR, page.open: ' + status);
                page.close();
                WaitForInput(system, console);
            } else {
                if (mode == "test") {
                    if (globalTimeout != null) {
                        clearTimeout(globalTimeout);
                        globalTimeout = null;
                    }

                    page.render(output);
                    page.close();
                    console.log('SUCCESS');

                    WaitForInput(system, console);
                }
            }
        });
    } finally {
        globalTimeout = setTimeout(function () {
            console.log("Max execution time - 10 seconds - exceeded");
            globalTimeout = null;
            page.close();
            WaitForInput(system, console);
        }, 10000);
    }
}

var system = require('system');

function WaitForInput(system, console) {
	while(true) {
	   var line = system.stdin.readLine();
	   if(line == "exit") 
	   {
			phantom.exit(0);
			return;
	   }
	   
	   var parameters = line.split("|");
	   if(parameters.length == 4) {
	   
		  var cookieInfo = parameters[0];
		  var url = parameters[1];
		  var outputFilePath = parameters[2];
		  var mode = parameters[3];
		  
		  var cookie = null;
		  
		  if(cookieInfo != "") {
			var cookieInfoParts = cookieInfo.split(",");
			
			if(cookieInfoParts.length != 3) {
				console.log('Invalid cookie information, correct format is {name},{value},{domain}');
				continue;
			}
		
			cookie = {
				'name': cookieInfoParts[0],
				'value': cookieInfoParts[1],
				'domain': cookieInfoParts[2],
				'path': '/',
				'httponly': true,
				'secure':   false,
				'expires':  (new Date()).getTime() + (1000 * 60 * 60)
			};
		  }
		  		  	   
		  BuildFunctionPreview(system, console, url, outputFilePath, cookie, mode);
		  return;
	   }
	   else {
		  console.log('Usage: {Authentication cookie information}|{url}|{out put file name}. Where {Authentication cookie information} = {name},{value},{domain}');
	   }
   }
}

WaitForInput(system, console);