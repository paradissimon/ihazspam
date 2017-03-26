#!/bin/sh
SRC=/cygdrive/c/dev/IHazSpam/src
OUT=/cygdrive/c/IHazSpam
OUTWIN="c:\\IHazSpam"
pwd=`pwd`

echo ">>>>>>>> Cleaning output"
rm -rf $OUT/*

cd $SRC
for prj in MX MailExtractor Web MailCleaner; do   
	echo; echo ">>>>>>>> $prj"; echo;
	cd $SRC/$prj;
	dotnet publish --configuration release --framework netcoreapp1.1 --build-base-path "$OUTWIN\\build\\$prj" --output "$OUTWIN\\bin\\$prj";
done
 
cp -R $SRC/../stuff $OUT
cp -R $SRC/../data $OUT
rm -rf $OUT/build

echo ">>>>>>>> Compressing wwwroot assets"
(cd $OUT/bin/Web/wwwroot; find . -type f -exec gzip -9 --keep {} +)
(cd $OUT/bin/Web/wwwroot; find . -exec touch -t `date +%Y%m%d%H%M` {} +)


echo ">>>>>>>> Done"
cd $pwd