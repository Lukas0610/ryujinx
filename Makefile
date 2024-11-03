.PHONY: all
all: clean win-x64-sf win-x64-r2r win-x64-r2rsf

clean: clean-win-x64-sf clean-win-x64-r2r clean-win-x64-r2rsf

# win-x64 (Single-File)
win-x64-sf: clean-win-x64-sf
	dotnet publish \
	    -c Release \
	    -r win-x64 \
	    -o build/win-x64-sf \
	    -p:PublishReadyToRun=false \
	    -p:PublishSingleFile=true \
	    -p:DebugType=embedded \
	    --self-contained true \
	    src/Ryujinx/Ryujinx.csproj

clean-win-x64-sf:
	rm -rvf build/win-x64-sf

# win-x64 (Ready-To-Run)
win-x64-r2r: clean-win-x64-r2r
	dotnet publish \
	    -c Release \
	    -r win-x64 \
	    -o build/win-x64-r2r \
	    -p:PublishReadyToRun=true \
	    -p:PublishSingleFile=false \
	    -p:DebugType=embedded \
	    --self-contained true \
	    src/Ryujinx/Ryujinx.csproj

clean-win-x64-r2r:
	rm -rvf build/win-x64-r2r

# win-x64 (Ready-To-Run + Single-File)
win-x64-r2rsf: clean-win-x64-r2rsf
	dotnet publish \
	    -c Release \
	    -r win-x64 \
	    -o build/win-x64-r2rsf \
	    -p:PublishReadyToRun=true \
	    -p:PublishSingleFile=true \
	    -p:DebugType=embedded \
	    --self-contained true \
	    src/Ryujinx/Ryujinx.csproj

clean-win-x64-r2rsf:
	rm -rvf build/win-x64-r2rsf
