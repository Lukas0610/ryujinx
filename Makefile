.PHONY: all

all: clean build-win-x64 build-linux-x64
release: clean package-win-x64 package-linux-x64 package-linux-arm64 package-osx-arm64
clean: clean-win-x64 clean-linux-x64 clean-linux-arm64 clean-osx-arm64

###########################################
## win-x64
##
build-win-x64: clean-win-x64
	dotnet publish \
	    -c Release \
	    -r win-x64 \
	    -o build/win-x64 \
	    -p:PublishReadyToRun=true \
	    -p:PublishSingleFile=false \
	    -p:DebugType=embedded \
	    --self-contained true \
	    src/Ryujinx/Ryujinx.csproj

package-win-x64: clean-win-x64 build-win-x64
	cd build/win-x64/ && zip -r9 ../Ryujinx-Windows-x64.zip .

clean-win-x64:
	rm -rvf build/win-x64
	rm -vf build/Ryujinx-Windows-x64.zip

###########################################
## linux-x64
##
build-linux-x64: clean-linux-x64
	dotnet publish \
	    -c Release \
	    -r linux-x64 \
	    -o build/linux-x64 \
	    -p:PublishReadyToRun=true \
	    -p:PublishSingleFile=false \
	    -p:DebugType=embedded \
	    --self-contained true \
	    src/Ryujinx/Ryujinx.csproj

package-linux-x64: clean-linux-x64 build-linux-x64
	cd build/linux-x64/ && tar -cvaf ../Ryujinx-Linux-x64.tgz *

clean-linux-x64:
	rm -rvf build/linux-x64
	rm -vf build/Ryujinx-Linux-x64.tgz

###########################################
## linux-arm64
##
build-linux-arm64: clean-linux-arm64
	dotnet publish \
	    -c Release \
	    -r linux-arm64 \
	    -o build/linux-arm64 \
	    -p:PublishReadyToRun=true \
	    -p:PublishSingleFile=false \
	    -p:DebugType=embedded \
	    --self-contained true \
	    src/Ryujinx/Ryujinx.csproj

package-linux-arm64: clean-linux-arm64 build-linux-arm64
	cd build/linux-arm64/ && tar -cvaf ../Ryujinx-Linux-arm64.tgz *

clean-linux-arm64:
	rm -rvf build/linux-arm64
	rm -vf build/Ryujinx-Linux-arm64.tgz

###########################################
## osx-arm64
##
build-osx-arm64: clean-osx-arm64
	dotnet publish \
	    -c Release \
	    -r osx-arm64 \
	    -o build/osx-arm64 \
	    -p:PublishReadyToRun=true \
	    -p:PublishSingleFile=false \
	    -p:DebugType=embedded \
	    --self-contained true \
	    src/Ryujinx/Ryujinx.csproj

package-osx-arm64: clean-osx-arm64 build-osx-arm64
	cd build/osx-arm64/ && tar -cvaf ../Ryujinx-OSX-arm64.tgz *

clean-osx-arm64:
	rm -rvf build/osx-arm64
	rm -vf build/Ryujinx-OSX-arm64.tgz
