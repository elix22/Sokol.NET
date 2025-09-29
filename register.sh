#!/usr/bin/env bash

# Copyright (c) 2020-2025 Eli Aloni a.k.a elix22.
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in
# all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
# THE SOFTWARE.

#This script will create an folder called '.sokolnet_config' in the home folder
#It will create configuration files in that folder , to allow  proper functionality for Sokol.Net
#Each time that the Sokol.Net folder is moved to a different folder , this script must be called .

#Find out which OS we're on. 
unamestr=$(uname)

# Switch-on alias expansion within the script 
shopt -s expand_aliases

#Alias the sed in-place command for OSX and Linux - incompatibilities between BSD and Linux sed args
if [[ "$unamestr" == "Darwin" ]]; then
	alias aliassedinplace='sed -i ""'
else
	#For Linux, notice no space after the '-i' 
	alias aliassedinplace='sed -i""'
fi

currPwd=`pwd`

SOKOLNET_CONFIG_FOLDER=.sokolnet_config
HOME=~

rm -rf  ~/${SOKOLNET_CONFIG_FOLDER}

mkdir -p  ~/${SOKOLNET_CONFIG_FOLDER}
cp templates/SokolNetHome.config ~/${SOKOLNET_CONFIG_FOLDER}/

if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
	currWinPwd=$(cygpath -m ${currPwd})
	aliassedinplace "s*TEMPLATE_SOKOLNET_HOME*$currWinPwd*g" "$HOME/${SOKOLNET_CONFIG_FOLDER}/SokolNetHome.config"
else
	aliassedinplace "s*TEMPLATE_SOKOLNET_HOME*$currPwd*g" "$HOME/${SOKOLNET_CONFIG_FOLDER}/SokolNetHome.config"
fi

if [ -f ~/${SOKOLNET_CONFIG_FOLDER}/sokolnet_home ]; then
    rm -f ~/${SOKOLNET_CONFIG_FOLDER}/sokolnet_home
fi

touch ~/${SOKOLNET_CONFIG_FOLDER}/sokolnet_home
echo $currPwd >> ~/${SOKOLNET_CONFIG_FOLDER}/sokolnet_home

if [[ -f ~/.sokolnet_config/sokolnet_home  &&  -f ~/.sokolnet_config/SokolNetHome.config ]]; then
	echo ""
	echo "Sokol.Net configured!"
	echo ""
	SOKOLNET_HOME=$(cat ~/.sokolnet_config/sokolnet_home)
	SOKOLNET_HOME_XML=$(cat ~/.sokolnet_config/SokolNetHome.config)
	echo "cat ${HOME}/.sokolnet_config/sokolnet_home"
	echo "${SOKOLNET_HOME}"
	echo ""
	echo "cat ${HOME}/.sokolnet_config/SokolNetHome.config"
	echo "${SOKOLNET_HOME_XML}"
	echo ""
else
	echo "Sokol.Net configuration failure!"
fi

# read -p "getk: " getk